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
        => await _dbContext.Suppliers
            .Include(s => s.Accounts)
            .Include(s => s.SupplierGroup)
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

    public async Task<IReadOnlyList<Supplier>> GetByIdsAsync(
        IReadOnlyList<Guid> supplierIds,
        CancellationToken cancellationToken = default)
        => await _dbContext.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<Supplier>> GetRecentAsync(
        PagedRequest paging,
        Guid? supplierGroupId = null,
        bool? onHold = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Suppliers
            .Include(s => s.SupplierGroup)
            .Include(s => s.Accounts)
            .AsQueryable();

        if (supplierGroupId.HasValue)
        {
            query = query.Where(s => s.SupplierGroupId == supplierGroupId.Value);
        }

        if (onHold.HasValue)
        {
            query = query.Where(s => s.OnHold == onHold.Value);
        }

        if (companyId.HasValue)
        {
            // "Suppliers this company can buy from": those with an explicit per-company mapping.
            query = query.Where(s => s.Accounts.Any(a => a.CompanyId == companyId.Value));
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .ToPagedResultAsync(paging, cancellationToken);
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        var root = _dbContext.Entry(supplier);

        // New per-company mappings created by Supplier.SetCompanyAccount carry a client-generated
        // Guid key, so EF cannot tell them apart from an existing row once the graph is attached or
        // updated. Snapshot them while still Detached and classify them as Added afterwards.
        var pendingAccounts = supplier.Accounts
            .Where(a => _dbContext.Entry(a).State == EntityState.Detached)
            .ToList();

        // The supplier is normally loaded through GetByIdAsync (already tracked); the fallback keeps
        // a detached caller working the same way the previous Update(...) implementation did.
        if (root.State == EntityState.Detached)
        {
            _dbContext.Suppliers.Update(supplier);
        }

        foreach (var account in pendingAccounts)
        {
            _dbContext.Entry(account).State = EntityState.Added;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            root.State = EntityState.Detached;
            throw new InvalidOperationException("concurrency_conflict");
        }
    }
}
