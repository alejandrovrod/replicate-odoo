using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWarehouseRepository"/>. The ancestor walk mirrors
/// AccountRepository.GetByIdWithAncestorsAsync (cycle prevention for the warehouse tree).
/// CompanyId predicates are business scoping, not tenancy (Constitution II.3 stays automatic).
/// </summary>
public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly AppDbContext _dbContext;

    public WarehouseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        await _dbContext.Warehouses.AddAsync(warehouse, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Warehouse?> GetByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);

    public async Task<IReadOnlyList<Warehouse>> GetByIdWithAncestorsAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var chain = new List<Warehouse>();
        var visited = new HashSet<Guid>();
        Guid? nextId = warehouseId;

        while (nextId is { } id)
        {
            if (visited.Contains(id))
            {
                // The stored parent chain loops back on itself (data corruption). Append the
                // repeated node so WarehouseValidator.EnsureNoCycle sees the duplicate and fails
                // the request instead of walking forever.
                var repeated = chain.First(w => w.Id == id);
                chain.Add(repeated);
                break;
            }

            visited.Add(id);

            var current = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
            if (current is null)
            {
                break;
            }

            chain.Add(current);
            nextId = current.ParentWarehouseId;
        }

        return chain;
    }

    public async Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.Warehouses
            .Where(w => w.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<Warehouse>> GetFlatWarehousesAsync(
        Guid companyId,
        bool leavesOnly,
        bool? isActive,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Warehouses.Where(w => w.CompanyId == companyId);

        if (leavesOnly)
        {
            query = query.Where(w => !w.IsGroup);
        }

        if (isActive is not null)
        {
            query = query.Where(w => w.IsActive == isActive);
        }

        return await query
            .OrderBy(w => w.WarehouseCode)
            .ThenBy(w => w.Id)
            .ToPagedResultAsync(paging, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default)
        => _dbContext.Warehouses.AnyAsync(
            w => w.CompanyId == companyId && w.WarehouseCode == code,
            cancellationToken);

    public async Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another request touched this warehouse between our load and our save (the handler
            // already fails fast on a stale token, but a race can still slip through) - the
            // RowVersion WHERE clause matched 0 rows. Translate into the typed domain failure
            // the handler converts into a 409 Result.Failure, mirroring JournalRepository.
            throw new ConcurrencyConflictException(nameof(Warehouse), warehouse.Id, ex);
        }
    }
}
