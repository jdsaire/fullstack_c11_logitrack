using LogiTrack.Data;
using LogiTrack.Dtos;
using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext _context;

    public OrderController(LogiTrackContext context)
    {
        _context = context;
    }

    /// <summary>Returns every order with its item count.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderSummaryResponse>>> GetAll()
    {
        var orders = await _context.Orders.Include(o => o.Items).OrderBy(o => o.OrderId).ToListAsync();
        return Ok(orders.Select(o => new OrderSummaryResponse(o.OrderId, o.CustomerName, o.DatePlaced, o.Items.Count)));
    }

    /// <summary>Returns one order with its items.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderId == id);
        return order is null ? NotFound() : Ok(OrderResponse.From(order));
    }

    /// <summary>Creates an order from items in stock that aren't on another order.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request)
    {
        var ids = request.ItemIds;
        if (ids.Count == 0)
        {
            ModelState.AddModelError(nameof(request.ItemIds), "An order needs at least one item.");
            return ValidationProblem();
        }

        if (ids.Distinct().Count() != ids.Count)
        {
            ModelState.AddModelError(nameof(request.ItemIds), "Each item can appear once.");
            return ValidationProblem();
        }

        var items = await _context.InventoryItems.Where(i => ids.Contains(i.ItemId)).ToListAsync();

        var missing = ids.Except(items.Select(i => i.ItemId)).ToList();
        if (missing.Count > 0)
        {
            ModelState.AddModelError(nameof(request.ItemIds), $"Items not found: {string.Join(", ", missing)}.");
            return ValidationProblem();
        }

        var taken = items.Where(i => i.OrderId is not null).Select(i => i.ItemId).ToList();
        if (taken.Count > 0)
        {
            ModelState.AddModelError(nameof(request.ItemIds), $"Items already on an order: {string.Join(", ", taken)}.");
            return ValidationProblem();
        }

        var order = new Order { CustomerName = request.CustomerName, DatePlaced = DateTime.UtcNow };
        foreach (var item in items)
        {
            order.AddItem(item);
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.OrderId }, OrderResponse.From(order));
    }

    /// <summary>Deletes an order by id. Its items go back to stock.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order is null)
        {
            return NotFound();
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
