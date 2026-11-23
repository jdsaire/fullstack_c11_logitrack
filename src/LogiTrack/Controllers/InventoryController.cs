using System.Diagnostics;
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
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(LogiTrackContext context, IMemoryCache cache, ILogger<InventoryController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Returns every inventory item, from memory for 30 seconds after the first read.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InventoryItemResponse>>> GetAll()
    {
        var watch = Stopwatch.StartNew();
        var fromDatabase = false;

        var items = await _cache.GetOrCreateAsync(InventoryCache.Key, async entry =>
        {
            fromDatabase = true;
            entry.AbsoluteExpirationRelativeToNow = InventoryCache.Lifetime;
            return await _context.InventoryItems
                .AsNoTracking()
                .OrderBy(i => i.ItemId)
                .Select(i => new InventoryItemResponse(i.ItemId, i.Name, i.Quantity, i.Location, i.OrderId))
                .ToListAsync();
        });

        watch.Stop();
        _logger.LogInformation("GET /api/inventory served from {Source} in {ElapsedMs:F2} ms",
            fromDatabase ? "database" : "cache", watch.Elapsed.TotalMilliseconds);

        return Ok(items);
    }

    /// <summary>Adds an item to the inventory. Manager only.</summary>
    [HttpPost]
    [Authorize(Roles = ManagerSeeder.ManagerRole)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InventoryItemResponse>> Create(CreateInventoryItemRequest request)
    {
        var item = new InventoryItem
        {
            Name = request.Name,
            Quantity = request.Quantity,
            Location = request.Location
        };

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        _cache.Remove(InventoryCache.Key);

        return CreatedAtAction(nameof(GetAll), null, InventoryItemResponse.From(item));
    }

    /// <summary>Removes an item by id. Manager only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = ManagerSeeder.ManagerRole)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();
        _cache.Remove(InventoryCache.Key);
        return NoContent();
    }
}
