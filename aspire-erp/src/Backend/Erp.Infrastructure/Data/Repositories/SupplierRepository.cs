using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISupplierRepository"/> (Task 4.1).
/// </summary>
/// <remarks>
/// There is deliberately NO manual <c>.Where(s =&gt; s.TenantId == ...)</c> on LINQ queries
/// (Constitution II.3 - the global query filter does it). Suppliers are tenant-wide, so reads
/// are not company-filtered (same as Items).
/// </remarks>
public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _dbContext;

    public SupplierRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        await _dbContext.Suppliers.AddAsync(supplier, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _dbContext.Suppliers.AnyAsync(s => s.Code == code, cancellationToken);

    public async Task<Supplier?> GetByIdAsync(Guid supplierId, CancellationToken cancellationToken = default)
        => await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

    public async Task<IReadOnlyList<Supplier>> GetByIdsAsync(
        IReadOnlyList<Guid> supplierIds,
        CancellationToken cancellationToken = default)
        => await _dbContext.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<Supplier>> GetRecentAsync(PagedRequest paging, CancellationToken cancellationToken = default)
        => await _dbContext.Suppliers
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .ToPagedResultAsync(paging, cancellationToken);

    public async Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        _dbContext.Suppliers.Update(supplier);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.Entry(supplier).State = EntityState.Detached;
            throw new InvalidOperationException("concurrency_conflict");
        }
    }
}
