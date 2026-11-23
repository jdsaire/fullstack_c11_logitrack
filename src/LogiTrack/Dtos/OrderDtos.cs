using LogiTrack.Models;

namespace LogiTrack.Dtos;

/// <summary>Body of POST /api/orders: a customer and the ids of items in stock that aren't on another order.</summary>
public class CreateOrderRequest
{
    public string CustomerName { get; set; } = string.Empty;

    public List<int> ItemIds { get; set; } = new();
}

/// <summary>One row of GET /api/orders.</summary>
public record OrderSummaryResponse(int OrderId, string CustomerName, DateTime DatePlaced, int ItemCount);

/// <summary>An order with its items, as GET /api/orders/{id} returns it.</summary>
public record OrderResponse(
    int OrderId,
    string CustomerName,
    DateTime DatePlaced,
    string Summary,
    IReadOnlyList<InventoryItemResponse> Items)
{
    public static OrderResponse From(Order order) =>
        new(order.OrderId, order.CustomerName, order.DatePlaced, order.GetOrderSummary(),
            order.Items.Select(InventoryItemResponse.From).ToList());
}
