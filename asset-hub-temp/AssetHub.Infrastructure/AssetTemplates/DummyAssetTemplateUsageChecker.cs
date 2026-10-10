using System;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Infrastructure.AssetTemplates;

public class DummyAssetTemplateUsageChecker : IAssetTemplateUsageChecker
{
    private readonly ITenantDbContext _dbContext;

    public DummyAssetTemplateUsageChecker(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GetUsageCountAsync(Guid assetTemplateId)
    {
        return await _dbContext.Assets.CountAsync(a => a.AssetTemplateId == assetTemplateId);
    }
}
