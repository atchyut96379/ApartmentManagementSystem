using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Filters;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<MustChangePasswordFilter>();
});
builder.Services.AddScoped<ResidentProfileService>();
builder.Services.AddScoped<DashboardStatsService>();
builder.Services.AddScoped<MaintenanceBillingService>();
builder.Services.AddScoped<ResidentImportService>();
builder.Services.AddScoped<ResidentAccountService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
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

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    if (app.Environment.IsDevelopment())
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "Resident" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var residentProfileService = services.GetRequiredService<ResidentProfileService>();
    foreach (var role in new[] { "Resident", "Admin" })
    {
        var usersInRole = await userManager.GetUsersInRoleAsync(role);
        foreach (var user in usersInRole)
        {
            await residentProfileService.EnsureResidentProfileAsync(user);
        }
    }

    var billingService = services.GetRequiredService<MaintenanceBillingService>();
    await billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();
}
app.Run();