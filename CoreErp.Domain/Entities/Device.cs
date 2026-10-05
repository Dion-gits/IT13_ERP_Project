namespace CoreErp.Domain.Entities;

public class Device
{
    public int DeviceId { get; set; }
    public int CompanyId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Company? Company { get; set; }
}