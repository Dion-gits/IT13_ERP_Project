namespace CoreErp.Domain.Entities;

public class Sale
{
    public int SaleId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int? CashierId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeDue { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
}