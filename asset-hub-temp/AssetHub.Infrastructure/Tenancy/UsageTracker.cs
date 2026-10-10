using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Infrastructure.Tenancy;

public class UsageTracker : IUsageTracker
{
    private readonly SecurityDbContext _securityDb;
    private readonly TenantDbContext _tenantDb;
    private readonly ITenantResolver _tenantResolver;

    public UsageTracker(SecurityDbContext securityDb, TenantDbContext tenantDb, ITenantResolver tenantResolver)
    {
        _securityDb = securityDb;
        _tenantDb = tenantDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<int> GetCurrentUsersCountAsync()
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (tenantId == null) return 0;

        return await _securityDb.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive);
    }

    public Task<int> GetCurrentAssetsCountAsync()
    {
        // TODO: Leer Assets desde TenantDbContext cuando se implemente M8
        return Task.FromResult(0);
    }

    public Task<long> GetCurrentStorageBytesAsync()
    {
        // TODO: Leer AssetAttachments desde TenantDbContext cuando se implemente M8
        return Task.FromResult(0L);
    }
}
