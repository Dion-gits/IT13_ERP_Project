namespace CoreErp.WinForms.Views.Auth;

public class LoginResult
{
    public int UserId { get; set; }
    public int? CompanyId { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsSuperAdmin { get; set; }
}