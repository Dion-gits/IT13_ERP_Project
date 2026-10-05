using CoreErp.Infrastructure.Data;

namespace CoreErp.Infrastructure.Services;

public interface ITenantDbContextFactory
{
    Task<TenantErpDbContext> CreateAsync(int companyId);
}