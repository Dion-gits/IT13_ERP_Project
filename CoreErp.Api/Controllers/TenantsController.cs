using CoreErp.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreErp.Api.Controllers;

[ApiController]
[Route("tenants")]
public sealed class TenantsController : ControllerBase
{
    private readonly MasterCoreErpDbContext _master;

    public TenantsController(MasterCoreErpDbContext master) => _master = master;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _master.Companies
            .Where(c => c.IsActive)
            .Join(_master.CompanyDatabases.Where(d => d.IsActive),
                  c => c.CompanyId, d => d.CompanyId,
                  (c, d) => new
                  {
                      d.CompanyId,
                      c.CompanyCode,
                      c.CompanyName,
                      d.DatabaseName
                  })
            .OrderBy(x => x.CompanyId)
            .ToListAsync();

        return Ok(list);
    }
}