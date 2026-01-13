using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Carzvo.Models;

namespace Carzvo.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Shipment> Shipments { get; set; }
        public DbSet<DriverApplication> DriverApplications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Настройка отношений для Shipment
            builder.Entity<Shipment>()
                .HasOne(s => s.User)
                .WithMany(u => u.CreatedShipments)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Shipment>()
                .HasOne(s => s.Driver)
                .WithMany(u => u.AssignedShipments)
                .HasForeignKey(s => s.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка отношений для DriverApplication
            builder.Entity<DriverApplication>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Создание индексов
            builder.Entity<Shipment>()
                .HasIndex(s => s.OrderNumber)
                .IsUnique();

            builder.Entity<Shipment>()
                .HasIndex(s => s.Status);

            builder.Entity<Shipment>()
                .HasIndex(s => s.PickupDate);

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.IsDriver);
        }
    }
}