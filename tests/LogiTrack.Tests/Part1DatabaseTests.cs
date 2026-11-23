using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Tests;

/// <summary>Part 1: the InitialCreate migration and the one-to-many relationship on a real SQLite file.</summary>
public class Part1DatabaseTests
{
    private string _dbPath = string.Empty;

    [SetUp]
    public void CreateDatabase()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"logitrack-part1-{Guid.NewGuid():N}.db");
        using var context = NewContext();
        context.Database.Migrate();
    }

    [TearDown]
    public void DeleteDatabase()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            File.Delete(_dbPath + suffix);
        }
    }

    private LogiTrackContext NewContext() =>
        new(new DbContextOptionsBuilder<LogiTrackContext>().UseSqlite($"Data Source={_dbPath}").Options);

    [Test]
    public void Migration_CreatesBothTables()
    {
        using var context = NewContext();

        Assert.That(context.Database.GetAppliedMigrations(), Has.Some.EndsWith("_InitialCreate"));
        Assert.That(context.InventoryItems.Count(), Is.Zero);
        Assert.That(context.Orders.Count(), Is.Zero);
    }

    [Test]
    public void Order_SavesAndReloadsWithItsItems()
    {
        int orderId;
        using (var context = NewContext())
        {
            var order = new Order { CustomerName = "Samir", DatePlaced = new DateTime(2025, 4, 5) };
            order.AddItem(new InventoryItem { Name = "Pallet Jack", Quantity = 12, Location = "Warehouse A" });
            order.AddItem(new InventoryItem { Name = "Hand Truck", Quantity = 4, Location = "Warehouse B" });
            context.Orders.Add(order);
            context.SaveChanges();
            orderId = order.OrderId;
        }

        using (var context = NewContext())
        {
            var reloaded = context.Orders.Include(o => o.Items).Single(o => o.OrderId == orderId);
            Assert.That(reloaded.Items.Select(i => i.Name), Is.EquivalentTo(new[] { "Pallet Jack", "Hand Truck" }));
            Assert.That(reloaded.GetOrderSummary(), Does.Contain("| Items: 2 |"));
        }
    }

    [Test]
    public void DeletingAnOrder_ReleasesItsItems()
    {
        int orderId;
        using (var context = NewContext())
        {
            var order = new Order { CustomerName = "Samir", DatePlaced = DateTime.UtcNow };
            order.AddItem(new InventoryItem { Name = "Pallet Jack", Quantity = 12, Location = "Warehouse A" });
            context.Orders.Add(order);
            context.SaveChanges();
            orderId = order.OrderId;
        }

        using (var context = NewContext())
        {
            context.Orders.Remove(context.Orders.Single(o => o.OrderId == orderId));
            context.SaveChanges();
        }

        using (var context = NewContext())
        {
            var item = context.InventoryItems.Single();
            Assert.That(item.OrderId, Is.Null);
        }
    }
}
