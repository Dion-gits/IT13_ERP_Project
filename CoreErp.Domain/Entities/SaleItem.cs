namespace CoreErp.Domain.Entities;

public class SaleItem
{
    public int SaleItemId { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }

    // Navigation
    public Sale? Sale { get; set; }
    public Product? Product { get; set; }
}