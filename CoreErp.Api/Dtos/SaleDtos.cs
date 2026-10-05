namespace CoreErp.Api.Dtos;

public class CreateSaleRequest
{
    public int? CashierId { get; set; }
    public string CashierName { get; set; } = "Cashier";
    public string DiscountType { get; set; } = "None";        // None, Senior, PWD
    public string PaymentMethod { get; set; } = "Cash";       // Cash, GCash
    public decimal AmountPaid { get; set; }
    public List<CreateSaleItem> Items { get; set; } = new();
}

public class CreateSaleItem
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class SaleReceiptDto
{
    public int SaleId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string CashierName { get; set; } = "";
    public string DiscountType { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeDue { get; set; }
    public string PaymentMethod { get; set; } = "";
    public DateTime SaleDate { get; set; }
    public List<SaleReceiptLineDto> Lines { get; set; } = new();
}

public class SaleReceiptLineDto
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}