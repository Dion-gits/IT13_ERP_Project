namespace CoreErp.Domain.Entities;

public class PlatformUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = "123123";
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "SuperAdmin";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}