using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>EF Core implementation of <see cref="IUomRepository"/> (tenant filter is automatic).</summary>
public sealed class UomRepository : IUomRepository
{
    private readonly AppDbContext _dbContext;

    public UomRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UOM?> GetByIdAsync(Guid uomId, CancellationToken cancellationToken = default)
        => _dbContext.UOMs.FirstOrDefaultAsync(u => u.Id == uomId, cancellationToken);
}
