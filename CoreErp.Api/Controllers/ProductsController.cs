using CoreErp.Api.Dtos;
using CoreErp.Domain.Entities;
using CoreErp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreErp.Api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public ProductsController(ITenantDbContextFactory factory)
    {
        _factory = factory;
    }

    // Archived products are hidden unless ?includeArchived=true (POS never asks for them).
    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId, [FromQuery] bool includeArchived = false)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var products = await db.Products
            .AsNoTracking()
            .Where(p => includeArchived || p.IsActive)
            .OrderBy(p => p.ProductName)
            .Select(p => new ProductWithStockDto
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                UnitPrice = p.UnitPrice,
                UnitsPerBox = p.UnitsPerBox,
                UnitOfMeasure = p.UnitOfMeasure,
                IsActive = p.IsActive,
                QuantityOnHand = db.Inventories.Where(i => i.ProductId == p.ProductId).Select(i => i.QuantityOnHand).FirstOrDefault(),
                ReorderLevel = db.Inventories.Where(i => i.ProductId == p.ProductId).Select(i => i.ReorderLevel).FirstOrDefault(),
                IsLowStock = db.Inventories.Where(i => i.ProductId == p.ProductId).Select(i => i.QuantityOnHand <= i.ReorderLevel).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(products);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] CreateProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProductName))
            return BadRequest(new { message = "Product name is required." });

        await using var db = await _factory.CreateAsync(companyId);

        // Auto-generate product code: parse highest existing PROD-XXXX
        var existingCodes = await db.Products
            .Where(p => p.ProductCode.StartsWith("PROD-"))
            .Select(p => p.ProductCode)
            .ToListAsync();

        int maxNum = 0;
        foreach (var code in existingCodes)
        {
            if (code.Length > 5 && int.TryParse(code.Substring(5), out int n))
            {
                if (n > maxNum) maxNum = n;
            }
        }

        int nextNumber = maxNum + 1;
        string autoCode = $"PROD-{nextNumber:D4}";

        // Safety: ensure no collision
        while (await db.Products.AnyAsync(p => p.ProductCode == autoCode))
        {
            nextNumber++;
            autoCode = $"PROD-{nextNumber:D4}";
        }

        var product = new Product
        {
            ProductCode = autoCode,
            ProductName = req.ProductName.Trim(),
            UnitPrice = req.UnitPrice,
            UnitsPerBox = req.UnitsPerBox,
            UnitOfMeasure = req.UnitOfMeasure,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Products.Add(product);

        // Use navigation so EF inserts Product first, then Inventory with the correct FK
        db.Inventories.Add(new Inventory
        {
            Product = product,
            QuantityOnHand = req.InitialStock,
            ReorderLevel = req.ReorderLevel,
            LastUpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        return Ok(new { productId = product.ProductId, productCode = product.ProductCode });
    }

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Update(int companyId, int productId, [FromBody] UpdateProductRequest req)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var product = await db.Products.FindAsync(productId);
        if (product == null) return NotFound(new { message = "Product not found." });

        if (await db.Products.AnyAsync(p => p.ProductCode == req.ProductCode && p.ProductId != productId))
            return BadRequest(new { message = $"Product code '{req.ProductCode}' already used." });

        product.ProductCode = req.ProductCode.Trim();
        product.ProductName = req.ProductName.Trim();
        product.UnitPrice = req.UnitPrice;
        product.UnitsPerBox = req.UnitsPerBox;
        product.UnitOfMeasure = req.UnitOfMeasure;

        var inv = await db.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (inv != null)
        {
            inv.ReorderLevel = req.ReorderLevel;
            inv.LastUpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    // Archive = soft delete. The product (and its sales history) stays, but it's hidden from POS.
    [HttpPost("{productId:int}/archive")]
    public Task<IActionResult> Archive(int companyId, int productId) => SetActive(companyId, productId, false);

    [HttpPost("{productId:int}/restore")]
    public Task<IActionResult> Restore(int companyId, int productId) => SetActive(companyId, productId, true);

    private async Task<IActionResult> SetActive(int companyId, int productId, bool isActive)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var product = await db.Products.FindAsync(productId);
        if (product == null) return NotFound(new { message = "Product not found." });

        product.IsActive = isActive;
        await db.SaveChangesAsync();
        return Ok();
    }

    // Hard delete is kept for the API, but the app now archives instead.
    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Delete(int companyId, int productId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var product = await db.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var inv = await db.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (inv != null) db.Inventories.Remove(inv);
        db.Products.Remove(product);

        await db.SaveChangesAsync();
        return Ok();
    }
}