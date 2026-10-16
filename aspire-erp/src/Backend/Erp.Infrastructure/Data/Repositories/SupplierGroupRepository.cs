using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISupplierGroupRepository"/> (module buying-suppliers,
/// spec §1 gap D1).
/// </summary>
/// <remarks>
/// No manual tenant predicate: the AppDbContext global query filter scopes every read
/// (Constitution II.3).
/// </remarks>
public sealed class SupplierGroupRepository : ISupplierGroupRepository
{
    private readonly AppDbContext _dbContext;

    public SupplierGroupRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(SupplierGroup group, CancellationToken cancellationToken = default)
    {
        await _dbContext.SupplierGroups.AddAsync(group, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default)
        => _dbContext.SupplierGroups.AnyAsync(g => g.Code == code, cancellationToken);

    public Task<SupplierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.SupplierGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SupplierGroup>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _dbContext.SupplierGroups
            .OrderByDescending(g => g.IsActive)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);
}
