using CoreErp.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreErp.Api.Controllers;

[ApiController]
[Route("superadmin")]
public sealed class SuperAdminController : ControllerBase
{
    private readonly MasterCoreErpDbContext _master;

    public SuperAdminController(MasterCoreErpDbContext master) => _master = master;

    /// <summary>Counts for the dashboard header.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var totalTenants = await _master.Companies.CountAsync();
        var activeTenants = await _master.Companies.CountAsync(c => c.IsActive);
        var totalDevices = await _master.Devices.CountAsync();
        var platformAdmins = await _master.PlatformUsers.CountAsync(u => u.IsActive);

        return Ok(new
        {
            totalTenants,
            activeTenants,
            totalDevices,
            platformAdmins
        });
    }

    /// <summary>Every tenant with its DB mapping and device count.</summary>
    [HttpGet("companies")]
    public async Task<IActionResult> Companies()
    {
        var rows = await _master.Companies
            .OrderBy(c => c.CompanyId)
            .Select(c => new
            {
                c.CompanyId,
                c.CompanyCode,
                c.CompanyName,
                c.ContactEmail,
                c.IsActive,
                c.CreatedAt,
                Databases = _master.CompanyDatabases
                    .Where(d => d.CompanyId == c.CompanyId)
                    .Select(d => new
                    {
                        d.CompanyDatabaseId,
                        d.ServerName,
                        d.DatabaseName,
                        d.CredentialKey,
                        d.IsActive
                    })
                    .ToList(),
                DeviceCount = _master.Devices.Count(d => d.CompanyId == c.CompanyId),
                ActiveDeviceCount = _master.Devices.Count(d => d.CompanyId == c.CompanyId && d.IsActive)
            })
            .AsNoTracking()
            .ToListAsync();

        return Ok(rows);
    }

    /// <summary>All platform-level users (SuperAdmins).</summary>
    [HttpGet("platform-users")]
    public async Task<IActionResult> PlatformUsers()
    {
        var users = await _master.PlatformUsers
            .OrderBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt
            })
            .AsNoTracking()
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>Toggle a tenant's active flag.</summary>
    [HttpPost("companies/{companyId:int}/toggle-active")]
    public async Task<IActionResult> ToggleCompanyActive(int companyId)
    {
        var company = await _master.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
        if (company == null) return NotFound(new { message = "Company not found." });

        company.IsActive = !company.IsActive;
        await _master.SaveChangesAsync();
        return Ok(new { company.CompanyId, company.IsActive });
    }
}