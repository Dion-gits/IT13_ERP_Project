namespace CoreErp.Infrastructure.Services;

public interface ITenantDatabaseResolver
{
    Task<TenantDatabaseInfo> GetDatabaseInfoAsync(int companyId);
}