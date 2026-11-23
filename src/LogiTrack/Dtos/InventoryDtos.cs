using LogiTrack.Models;

namespace LogiTrack.Dtos;

/// <summary>Body of POST /api/inventory.</summary>
public class CreateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string Location { get; set; } = string.Empty;
}

/// <summary>An inventory item as the API returns it.</summary>
public record InventoryItemResponse(int ItemId, string Name, int Quantity, string Location, int? OrderId)
{
    public static InventoryItemResponse From(InventoryItem item) =>
        new(item.ItemId, item.Name, item.Quantity, item.Location, item.OrderId);
}
