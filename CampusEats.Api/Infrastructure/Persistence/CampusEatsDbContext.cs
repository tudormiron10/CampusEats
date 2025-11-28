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
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<DietaryTag> DietaryTags { get; set; }
    public DbSet<MenuItemDietaryTag> MenuItemDietaryTags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurăm relația 1-la-1 dintre User și Loyalty
        modelBuilder.Entity<User>()
            .HasOne(u => u.Loyalty)
            .WithOne(l => l.User)
            .HasForeignKey<Loyalty>(l => l.UserId);

        // Configurăm relația 1-la-mulți dintre Order și OrderItem
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.MenuItem)
            .WithMany() // MenuItem doesn't have OrderItems navigation
            .HasForeignKey(oi => oi.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Convertim enumerările în string-uri în baza de date
        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>(); //

        modelBuilder.Entity<Payment>()
            .Property(p => p.Status)
            .HasConversion<string>();

        // Configure many-to-many relationship between MenuItem and DietaryTag
        modelBuilder.Entity<MenuItemDietaryTag>()
            .HasKey(mdt => new { mdt.MenuItemId, mdt.DietaryTagId });

        modelBuilder.Entity<MenuItemDietaryTag>()
            .HasOne(mdt => mdt.MenuItem)
            .WithMany(m => m.MenuItemDietaryTags)
            .HasForeignKey(mdt => mdt.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MenuItemDietaryTag>()
            .HasOne(mdt => mdt.DietaryTag)
            .WithMany(dt => dt.MenuItemDietaryTags)
            .HasForeignKey(mdt => mdt.DietaryTagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}