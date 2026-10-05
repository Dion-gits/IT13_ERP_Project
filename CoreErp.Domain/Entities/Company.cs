namespace CoreErp.Domain.Entities;

public class Company
{
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation — will populate as we add more entities
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}