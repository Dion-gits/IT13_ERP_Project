using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoreErp.Infrastructure.Data;

public class TenantErpDbContextFactory : IDesignTimeDbContextFactory<TenantErpDbContext>
{
    public TenantErpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantErpDbContext>();

        // Local design-time connection (used only by `dotnet ef migrations`)
        optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;Database=db71781;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;");

        return new TenantErpDbContext(optionsBuilder.Options);
    }
}