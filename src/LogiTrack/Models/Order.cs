using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace LogiTrack.Models;

/// <summary>A customer order holding one or more inventory items.</summary>
public class Order
{
    [Key]
    public int OrderId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public DateTime DatePlaced { get; set; }

    /// <summary>The items in the order.</summary>
    public List<InventoryItem> Items { get; set; } = new();

    /// <summary>Adds an item once; adding the same item again does nothing.</summary>
    public void AddItem(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Saved items are matched by id; unsaved ones (id 0) by reference.
        bool alreadyInOrder = item.ItemId == 0
            ? Items.Contains(item)
            : Items.Exists(i => i.ItemId == item.ItemId);

        if (!alreadyInOrder)
        {
            Items.Add(item);
        }
    }

    /// <summary>Removes the item with this id. Returns false when the order doesn't hold it.</summary>
    public bool RemoveItem(int itemId) => Items.RemoveAll(i => i.ItemId == itemId) > 0;

    /// <summary>Returns e.g. "Order #1001 for Samir | Items: 2 | Placed: 4/5/2025".</summary>
    public string GetOrderSummary() =>
        $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | " +
        $"Placed: {DatePlaced.ToString("M/d/yyyy", CultureInfo.InvariantCulture)}";
}
