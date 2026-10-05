using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoreErp.Infrastructure.Data;

public class MasterCoreErpDbContextFactory : IDesignTimeDbContextFactory<MasterCoreErpDbContext>
{
    public MasterCoreErpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MasterCoreErpDbContext>();

        // Local design-time connection (used only by `dotnet ef migrations`)
        optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;Database=db71780;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;");

        return new MasterCoreErpDbContext(optionsBuilder.Options);
    }
}