using LogiTrack.Models;

namespace LogiTrack.Data;

/// <summary>Development-only sample data and the Part 1 console check.</summary>
public static class DevSeeder
{
    public static void Seed(LogiTrackContext context)
    {
        // Add test inventory item if none exist.
        if (!context.InventoryItems.Any())
        {
            context.InventoryItems.Add(new InventoryItem
            {
                Name = "Pallet Jack",
                Quantity = 12,
                Location = "Warehouse A"
            });

            context.SaveChanges();
        }

        // Retrieve and print inventory to confirm.
        foreach (var item in context.InventoryItems.ToList())
        {
            item.DisplayInfo(); // Item: Pallet Jack | Quantity: 12 | Location: Warehouse A
        }

        // Order check, in memory only: add three items, remove one, print the summary.
        var order = new Order { OrderId = 1001, CustomerName = "Samir", DatePlaced = new DateTime(2025, 4, 5) };
        order.AddItem(new InventoryItem { ItemId = 1, Name = "Pallet Jack", Quantity = 12, Location = "Warehouse A" });
        order.AddItem(new InventoryItem { ItemId = 2, Name = "Hand Truck", Quantity = 4, Location = "Warehouse B" });
        order.AddItem(new InventoryItem { ItemId = 3, Name = "Stretch Wrap", Quantity = 30, Location = "Warehouse A" });
        order.RemoveItem(3);

        Console.WriteLine(order.GetOrderSummary()); // Order #1001 for Samir | Items: 2 | Placed: 4/5/2025
    }
}
