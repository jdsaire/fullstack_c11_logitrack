using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;

/// <summary>A stock item held at a fulfillment center.</summary>
public class InventoryItem
{
    [Key]
    public int ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    [Required]
    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    /// <summary>The order holding this item, if any (one order, many items).</summary>
    public int? OrderId { get; set; }

    /// <summary>Prints the item, e.g. "Item: Pallet Jack | Quantity: 12 | Location: Warehouse A".</summary>
    public void DisplayInfo() =>
        Console.WriteLine($"Item: {Name} | Quantity: {Quantity} | Location: {Location}");
}
