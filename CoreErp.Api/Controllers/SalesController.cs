using CoreErp.Api.Dtos;
using CoreErp.Domain.Entities;
using CoreErp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreErp.Api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private const decimal SENIOR_PWD_DISCOUNT = 0.20m;   // 20%

    public SalesController(ITenantDbContextFactory factory)
    {
        _factory = factory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
    int companyId,
    [FromQuery] string? search,
    [FromQuery] string? payment,
    [FromQuery] string? dateRange)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.Sales
            .Include(s => s.SaleItems)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.InvoiceNumber.ToLower().Contains(term) ||
                s.CashierName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(payment) && payment != "All")
        {
            query = query.Where(s => s.PaymentMethod == payment);
        }

        var now = DateTime.UtcNow;
        query = dateRange switch
        {
            "Today" => query.Where(s => s.SaleDate.Date == now.Date),
            "Week" => query.Where(s => s.SaleDate >= now.AddDays(-7)),
            "Month" => query.Where(s => s.SaleDate >= now.AddDays(-30)),
            "Year" => query.Where(s => s.SaleDate >= now.AddDays(-365)),
            _ => query
        };

        var sales = await query
            .OrderByDescending(s => s.SaleDate)
            .Take(500)
            .Select(s => new
            {
                s.SaleId,
                s.InvoiceNumber,
                s.CashierName,
                s.PaymentMethod,
                s.DiscountType,
                s.Subtotal,
                s.DiscountAmount,
                s.TotalAmount,
                s.AmountPaid,
                s.ChangeDue,
                s.SaleDate,
                ItemCount = s.SaleItems.Count
            })
            .ToListAsync();

        return Ok(sales);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] CreateSaleRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "Cart is empty." });

        if (req.AmountPaid < 0)
            return BadRequest(new { message = "Invalid amount paid." });

        await using var db = await _factory.CreateAsync(companyId);
        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            // 1. Load products + inventory, validate stock
            var productIds = req.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await db.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId);

            var inventories = await db.Inventories
                .Where(i => productIds.Contains(i.ProductId))
                .ToDictionaryAsync(i => i.ProductId);

            decimal subtotal = 0;

            foreach (var item in req.Items)
            {
                if (item.Quantity <= 0)
                    return BadRequest(new { message = $"Invalid quantity for product {item.ProductId}." });

                if (!products.ContainsKey(item.ProductId))
                    return BadRequest(new { message = $"Product {item.ProductId} not found." });

                var inventory = inventories.GetValueOrDefault(item.ProductId);
                var available = inventory?.QuantityOnHand ?? 0;

                if (available < item.Quantity)
                {
                    return BadRequest(new
                    {
                        message = $"Insufficient stock for {products[item.ProductId].ProductName}. " +
                                  $"Available: {available}, requested: {item.Quantity}"
                    });
                }

                subtotal += item.Quantity * products[item.ProductId].UnitPrice;
            }

            // 2. Compute discount + total
            decimal discountAmount = req.DiscountType switch
            {
                "Senior" => Math.Round(subtotal * SENIOR_PWD_DISCOUNT, 2),
                "PWD" => Math.Round(subtotal * SENIOR_PWD_DISCOUNT, 2),
                _ => 0m
            };

            decimal total = subtotal - discountAmount;

            if (req.PaymentMethod == "Cash" && req.AmountPaid < total)
                return BadRequest(new { message = "Amount paid is less than total." });

            // 3. Create Sale
            var invoice = "INV-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

            var sale = new Sale
            {
                InvoiceNumber = invoice,
                CashierId = req.CashierId,
                CashierName = req.CashierName,
                Subtotal = subtotal,
                DiscountType = req.DiscountType,
                DiscountAmount = discountAmount,
                TotalAmount = total,
                AmountPaid = req.PaymentMethod == "Cash" ? req.AmountPaid : total,
                ChangeDue = req.PaymentMethod == "Cash" ? (req.AmountPaid - total) : 0,
                PaymentMethod = req.PaymentMethod,
                SaleDate = DateTime.UtcNow,
                IsActive = true
            };

            // 4. Build SaleItems + deduct stock
            var receiptLines = new List<SaleReceiptLineDto>();

            foreach (var item in req.Items)
            {
                var product = products[item.ProductId];
                var lineSubtotal = item.Quantity * product.UnitPrice;

                sale.SaleItems.Add(new SaleItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.UnitPrice,
                    SubTotal = lineSubtotal
                });

                var inventory = inventories[item.ProductId];
                inventory.QuantityOnHand -= item.Quantity;
                inventory.LastUpdatedAt = DateTime.UtcNow;

                receiptLines.Add(new SaleReceiptLineDto
                {
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = product.UnitPrice,
                    Subtotal = lineSubtotal
                });
            }

            db.Sales.Add(sale);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new SaleReceiptDto
            {
                SaleId = sale.SaleId,
                InvoiceNumber = sale.InvoiceNumber,
                CashierName = sale.CashierName,
                DiscountType = sale.DiscountType,
                Subtotal = sale.Subtotal,
                DiscountAmount = sale.DiscountAmount,
                TotalAmount = sale.TotalAmount,
                AmountPaid = sale.AmountPaid,
                ChangeDue = sale.ChangeDue,
                PaymentMethod = sale.PaymentMethod,
                SaleDate = sale.SaleDate,
                Lines = receiptLines
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = ex.Message });
        }
    }
}