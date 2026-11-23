using LogiTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data;

/// <summary>EF Core context for LogiTrack's SQLite database, including the Identity tables.</summary>
public class LogiTrackContext : IdentityDbContext<ApplicationUser>
{
    public LogiTrackContext(DbContextOptions<LogiTrackContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;

    public DbSet<Order> Orders { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // One order holds many items. Deleting an order releases its items back to stock.
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Dates are written in UTC; SQLite drops the kind, so it's restored on read.
        modelBuilder.Entity<Order>()
            .Property(o => o.DatePlaced)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
