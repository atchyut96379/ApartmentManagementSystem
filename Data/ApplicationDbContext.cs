using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Resident> Residents { get; set; }

        public DbSet<Maintenance> Maintenances { get; set; }

        public DbSet<Expense> Expenses { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Maintenance>()
                .Property(m => m.Amount)
                .HasPrecision(18, 2);

            builder.Entity<Expense>()
                .Property(e => e.Amount)
                .HasPrecision(18, 2);

            builder.Entity<AuditLog>(entity =>
            {
                entity.Property(e => e.ActorUserId).HasMaxLength(450);
                entity.Property(e => e.ActorDisplayName).HasMaxLength(200);
                entity.Property(e => e.Action).HasMaxLength(100);
                entity.Property(e => e.EntityType).HasMaxLength(100);
                entity.Property(e => e.FlatNumber).HasMaxLength(50);
                entity.Property(e => e.Details).HasMaxLength(2000);
                entity.HasIndex(e => e.CreatedAtUtc);
            });
        }
    }
}