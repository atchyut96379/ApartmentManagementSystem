using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Filters;
using ApartmentManagementSystem.Hubs;
using ApartmentManagementSystem.Identity;
using SystemAdminConstants = ApartmentManagementSystem.Identity.SystemAdminConstants;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using ApartmentManagementSystem.Services.Email;
using ApartmentManagementSystem.Services.Sms;
using ApartmentManagementSystem.Services.WhatsApp;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "integrations.local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.Configure<SocietySettings>(
    builder.Configuration.GetSection(SocietySettings.SectionName));
builder.Services.Configure<PaymentGatewaySettings>(
    builder.Configuration.GetSection(PaymentGatewaySettings.SectionName));
builder.Services.Configure<IdentitySeedSettings>(
    builder.Configuration.GetSection(IdentitySeedSettings.SectionName));
builder.Services.Configure<NotificationSettings>(
    builder.Configuration.GetSection(NotificationSettings.SectionName));
builder.Services.Configure<ApplicationSettings>(
    builder.Configuration.GetSection(ApplicationSettings.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<MustChangePasswordFilter>();
});
builder.Services.AddScoped<ResidentProfileService>();
builder.Services.AddScoped<DashboardStatsService>();
builder.Services.AddScoped<MaintenanceBillingService>();
builder.Services.AddScoped<ResidentImportService>();
builder.Services.AddScoped<ResidentAccountService>();
builder.Services.AddScoped<ResidentPasswordResetService>();
builder.Services.AddScoped<LoginIdentityService>();
builder.Services.AddScoped<ResidentLoginProvisioningService>();
builder.Services.AddScoped<ResidentBulkLoginService>();
builder.Services.AddScoped<DashboardReportExportService>();
builder.Services.AddScoped<IdentitySeedService>();
builder.Services.AddScoped<MaintenanceFineService>();
builder.Services.AddScoped<PaymentGatewayService>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<SmsSenderService>();
builder.Services.AddScoped<WhatsAppSenderService>();
builder.Services.AddScoped<PaymentReminderMessageBuilder>();
builder.Services.AddScoped<PaymentNotificationService>();
builder.Services.AddScoped<PaymentReceiptPdfService>();
builder.Services.AddScoped<PaymentRealtimeNotifier>();
builder.Services.AddSingleton<IntegrationsSettingsStore>();
builder.Services.AddScoped<IntegrationStatusService>();
builder.Services.AddScoped<MonthlyPaymentDetailsService>();
builder.Services.AddHostedService<PaymentReminderBackgroundService>();
builder.Services.AddScoped<SocietyDataResetService>();
builder.Services.AddScoped<CommitteeAccessService>();
builder.Services.AddScoped<CommitteeLoginProvisioningService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<PasswordResetOtpService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Allows default system admin password "Admin" (5 characters, no digit).
        options.Password.RequiredLength = 5;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";

    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();

app.MapHub<PaymentUpdatesHub>("/hubs/payments");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var skipMigrations = string.Equals(
        Environment.GetEnvironmentVariable("DATABASE_MIGRATE_ON_STARTUP"),
        "false",
        StringComparison.OrdinalIgnoreCase);

    if (!skipMigrations)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed.");
            throw;
        }
    }

    var identitySeed = services.GetRequiredService<IdentitySeedService>();
    var seedSettings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<IdentitySeedSettings>>().Value;

    if (seedSettings.ResetAndSeedAdminOnStartup)
    {
        var societyReset = services.GetRequiredService<SocietyDataResetService>();
        await identitySeed.ResetAllUsersAndSeedSystemAdminAsync(societyReset);
    }
    else
    {
        await identitySeed.EnsureSystemAdminExistsAsync();
    }

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var residentProfileService = services.GetRequiredService<ResidentProfileService>();
    foreach (var user in userManager.Users.ToList())
    {
        if (!SystemAdminConstants.IsSystemAdmin(user))
        {
            await residentProfileService.EnsureResidentProfileAsync(user);
        }
    }

    var billingService = services.GetRequiredService<MaintenanceBillingService>();
    await billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();
}
app.Run();