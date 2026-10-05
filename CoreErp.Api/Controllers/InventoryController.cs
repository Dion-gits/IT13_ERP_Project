using CoreErp.Api.Dtos;
using CoreErp.Domain.Entities;
using CoreErp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreErp.Api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public InventoryController(ITenantDbContextFactory factory)
    {
        _factory = factory;
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust(int companyId, [FromBody] AdjustStockRequest req)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var product = await db.Products.FindAsync(req.ProductId);
        if (product == null) return NotFound(new { message = "Product not found." });

        var inv = await db.Inventories.FirstOrDefaultAsync(i => i.ProductId == req.ProductId);
        if (inv == null)
        {
            inv = new Inventory
            {
                ProductId = req.ProductId,
                QuantityOnHand = req.QuantityDelta,
                ReorderLevel = req.ReorderLevel,
                LastUpdatedAt = DateTime.UtcNow
            };
            db.Inventories.Add(inv);
        }
        else
        {
            inv.QuantityOnHand += req.QuantityDelta;
            inv.ReorderLevel = req.ReorderLevel;
            inv.LastUpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new
        {
            productId = req.ProductId,
            newQuantity = inv.QuantityOnHand,
            reorderLevel = inv.ReorderLevel
        });
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var lowStock = await db.Inventories
            .Include(i => i.Product)
            .Where(i => i.QuantityOnHand <= i.ReorderLevel)
            .AsNoTracking()
            .Select(i => new
            {
                i.ProductId,
                ProductCode = i.Product!.ProductCode,
                ProductName = i.Product.ProductName,
                i.QuantityOnHand,
                i.ReorderLevel
            })
            .ToListAsync();

        return Ok(lowStock);
    }
}