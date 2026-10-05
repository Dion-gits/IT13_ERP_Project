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

using (var scope = app.Services.CreateScope())
{
    var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
    foreach (int companyId in new[] { 1, 2, 3 })
    {
        try
        {
            await using var tenantDb = await tenantFactory.CreateAsync(companyId);
            await tenantDb.Database.MigrateAsync();
            Console.WriteLine($"✅ Migrated tenant {companyId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Tenant {companyId} failed: {ex.Message}");
        }
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