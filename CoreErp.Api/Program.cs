using CoreErp.Domain.Entities;
using CoreErp.Infrastructure.Data;
using CoreErp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddDbContext<MasterCoreErpDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MasterErp"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddDbContext<TenantErpDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("TenantErp"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

builder.Services.AddOpenApi();

var app = builder.Build();

// ══════════════════════════════════════════════════════════════
// Startup: migrate + seed master, then migrate + seed each tenant
// ══════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var cfg = builder.Configuration;

    // 1 — Master DB
    try
    {
        var master = sp.GetRequiredService<MasterCoreErpDbContext>();
        await master.Database.MigrateAsync();
        await SeedMasterAsync(master, cfg);
        Console.WriteLine("✅ Master DB ready");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Master DB failed: {ex.Message}");
    }

    // 2 — Tenants (driven by CompanyDatabases, not a hardcoded list)
    try
    {
        var master = sp.GetRequiredService<MasterCoreErpDbContext>();
        var tenantFactory = sp.GetRequiredService<ITenantDbContextFactory>();

        var tenants = await master.CompanyDatabases
            .Where(x => x.IsActive)
            .Select(x => new { x.CompanyId, x.CredentialKey })
            .ToListAsync();

        foreach (var t in tenants)
        {
            try
            {
                await using var tenantDb = await tenantFactory.CreateAsync(t.CompanyId);
                await tenantDb.Database.MigrateAsync();
                await SeedTenantAsync(tenantDb, t.CompanyId);
                Console.WriteLine($"✅ Migrated tenant {t.CompanyId} ({t.CredentialKey})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Tenant {t.CompanyId} failed: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Tenant loop failed: {ex.Message}");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ══════════════════════════════════════════════════════════════
// Seed helpers
// ══════════════════════════════════════════════════════════════

static async Task SeedMasterAsync(MasterCoreErpDbContext master, IConfiguration cfg)
{
    var tenantsSection = cfg.GetSection("Tenants");
    if (!tenantsSection.Exists())
    {
        Console.WriteLine("⚠ No 'Tenants' config section — skipping master seed.");
        return;
    }

    bool changed = false;

    foreach (var child in tenantsSection.GetChildren())
    {
        if (!int.TryParse(child.Key, out int companyId)) continue;

        var server = child["Server"];
        var database = child["Database"];
        var cred = child["CredentialKey"] ?? $"Tenant{companyId}";
        var code = child["Code"] ?? $"TENANT{companyId}";
        var name = child["Name"] ?? $"Tenant {companyId}";

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
        {
            Console.WriteLine($"⚠ Tenant {companyId} missing Server/Database — skipped.");
            continue;
        }

        // ── Company ──
        var company = await master.Companies.FirstOrDefaultAsync(c => c.CompanyCode == code);
        if (company == null)
        {
            company = new Company
            {
                CompanyCode = code,
                CompanyName = name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            master.Companies.Add(company);
            await master.SaveChangesAsync();
            Console.WriteLine($"➕ Added company {code} (id={company.CompanyId})");
            changed = true;
        }
        else if (company.CompanyName != name || !company.IsActive)
        {
            company.CompanyName = name;
            company.IsActive = true;
            changed = true;
        }

        // ── CompanyDatabase ──
        var cdb = await master.CompanyDatabases.FirstOrDefaultAsync(c => c.CompanyId == company.CompanyId);
        if (cdb == null)
        {
            cdb = new CompanyDatabase
            {
                CompanyId = company.CompanyId,
                ServerName = server,
                DatabaseName = database,
                CredentialKey = cred,
                IsActive = true
            };
            master.CompanyDatabases.Add(cdb);
            await master.SaveChangesAsync();
            Console.WriteLine($"➕ Added DB mapping for company {company.CompanyId}: {server}/{database} ({cred})");
            changed = true;
        }
        else if (cdb.ServerName != server || cdb.DatabaseName != database
              || cdb.CredentialKey != cred || !cdb.IsActive)
        {
            cdb.ServerName = server;
            cdb.DatabaseName = database;
            cdb.CredentialKey = cred;
            cdb.IsActive = true;
            changed = true;
        }
    }

    if (changed) await master.SaveChangesAsync();
}

static async Task SeedTenantAsync(TenantErpDbContext db, int companyId)
{
    if (await db.Users.AnyAsync()) return;

    const string Pwd = "123123";
    db.Users.AddRange(
        new User { CompanyId = companyId, Email = $"owner@tenant{companyId}.local", FullName = "Owner", Role = "Owner", Password = Pwd },
        new User { CompanyId = companyId, Email = $"superadmin@tenant{companyId}.local", FullName = "Super Admin", Role = "SuperAdmin", Password = Pwd },
        new User { CompanyId = companyId, Email = $"branch@tenant{companyId}.local", FullName = "Branch Manager", Role = "BranchManager", Password = Pwd },
        new User { CompanyId = companyId, Email = $"hr@tenant{companyId}.local", FullName = "HR Manager", Role = "HrManager", Password = Pwd },
        new User { CompanyId = companyId, Email = $"inventory@tenant{companyId}.local", FullName = "Inventory Staff", Role = "InventoryStaff", Password = Pwd },
        new User { CompanyId = companyId, Email = $"cashier@tenant{companyId}.local", FullName = "Cashier", Role = "Cashier", Password = Pwd }
    );
    await db.SaveChangesAsync();
    Console.WriteLine($"👤 Seeded demo users for tenant {companyId}");
}