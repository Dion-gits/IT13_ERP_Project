namespace CoreErp.Api.Dtos;

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }     // still returned — tells the client which tenant this user belongs to
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
}