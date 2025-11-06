using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Infrastructure.Persistence;

public class CampusEatsDbContext : DbContext
{
    public CampusEatsDbContext(DbContextOptions<CampusEatsDbContext> options)
        : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Loyalty> Loyalties { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurăm relația 1-la-1 dintre User și Loyalty
        modelBuilder.Entity<User>()
            .HasOne(u => u.Loyalty)
            .WithOne(l => l.User)
            .HasForeignKey<Loyalty>(l => l.UserId);

        // Configurăm relația mulți-la-mulți dintre Order și MenuItem
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithMany(mi => mi.Orders);
            
        // Convertim enumerările în string-uri în baza de date
        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>(); //

        modelBuilder.Entity<Payment>()
            .Property(p => p.Status)
            .HasConversion<string>(); //
    }
}