using LogiTrack.Data;
using LogiTrack.Dtos;
using LogiTrack.Models;
using LogiTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers;

[ApiController]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;

    public OrderController(LogiTrackContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>Returns every order with its item count.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrderSummaryResponse>>> GetAll()
    {
        // The count is computed in SQL; the items themselves are never loaded for the list.
        var orders = await _context.Orders
            .AsNoTracking()
            .OrderBy(o => o.OrderId)
            .Select(o => new OrderSummaryResponse(o.OrderId, o.CustomerName, o.DatePlaced, o.Items.Count))
            .ToListAsync();
        return Ok(orders);
    }

    /// <summary>Returns one order with its items.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        return order is null ? NotFound() : Ok(OrderResponse.From(order));
    }

    /// <summary>Creates an order from items in stock that aren't on another order.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request)
    {
        var ids = request.ItemIds;
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

        var order = new Order
        {
            CustomerName = request.CustomerName,
            DatePlaced = DateTime.UtcNow,
            CreatedByUserId = User.FindFirst("sub")?.Value
        };
        foreach (var item in items)
        {
            order.AddItem(item);
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _cache.Remove(InventoryCache.Key); // the items' order id changed

        return CreatedAtAction(nameof(GetById), new { id = order.OrderId }, OrderResponse.From(order));
    }

    /// <summary>Deletes an order by id. Its items go back to stock. Manager only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = ManagerSeeder.ManagerRole)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
        _cache.Remove(InventoryCache.Key); // the items went back to stock
        return NoContent();
    }
}
