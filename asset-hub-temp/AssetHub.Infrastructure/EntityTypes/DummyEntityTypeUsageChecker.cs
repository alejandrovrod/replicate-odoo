using System;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Infrastructure.EntityTypes;

public class DummyEntityTypeUsageChecker : IEntityTypeUsageChecker
{
    private readonly ITenantDbContext _dbContext;

    public DummyEntityTypeUsageChecker(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GetUsageCountAsync(Guid entityTypeId)
    {
        return await _dbContext.AssetTemplates
            .CountAsync(t => t.BusinessEntityTypeId == entityTypeId);
    }
}
