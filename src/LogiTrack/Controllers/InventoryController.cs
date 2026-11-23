using LogiTrack.Data;
using LogiTrack.Dtos;
using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;

    public InventoryController(LogiTrackContext context)
    {
        _context = context;
    }

    /// <summary>Returns every inventory item.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItemResponse>>> GetAll()
    {
        var items = await _context.InventoryItems.OrderBy(i => i.ItemId).ToListAsync();
        return Ok(items.Select(InventoryItemResponse.From));
    }

    /// <summary>Adds an item to the inventory.</summary>
    [HttpPost]
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

        return CreatedAtAction(nameof(GetAll), null, InventoryItemResponse.From(item));
    }

    /// <summary>Removes an item by id.</summary>
    [HttpDelete("{id:int}")]
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
        return NoContent();
    }
}
