using CoreErp.Api.Dtos;
using CoreErp.Infrastructure.Data;
using CoreErp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreErp.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly MasterCoreErpDbContext _master;
    private readonly ITenantDbContextFactory _factory;

    public AuthController(MasterCoreErpDbContext master, ITenantDbContextFactory factory)
    {
        _master = master;
        _factory = factory;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Email and password are required." });

        var email = req.Email.Trim().ToLowerInvariant();

        // All active tenants registered in the master DB
        var companyIds = await _master.CompanyDatabases
            .Where(x => x.IsActive)
            .Select(x => x.CompanyId)
            .Distinct()
            .ToListAsync();

        // Scan each tenant for a matching email.
        // Small N (a handful of tenants for a medium enterprise), so a linear scan is fine.
        foreach (var companyId in companyIds)
        {
            try
            {
                await using var db = await _factory.CreateAsync(companyId);

                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

                if (user == null) continue;

                // NOTE: plaintext compare — swap for BCrypt/Identity hashing before going live.
                if (user.Password != req.Password || !user.IsActive)
                    return Unauthorized(new { message = "Invalid email or password." });

                user.LastLoginAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                return Ok(new LoginResponse
                {
                    UserId = user.UserId,
                    CompanyId = companyId,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role
                });
            }
            catch (Exception ex)
            {
                // A tenant DB might be offline — log and keep looking.
                Console.WriteLine($"⚠ Login lookup failed for tenant {companyId}: {ex.Message}");
                continue;
            }
        }

        return Unauthorized(new { message = "Invalid email or password." });
    }
}