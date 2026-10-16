using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ILandedCostVoucherRepository"/> (Fase 4).
/// No manual tenant predicates (Constitution II.3 - the global query filter scopes reads).
/// </summary>
public sealed class LandedCostVoucherRepository : ILandedCostVoucherRepository
{
    private readonly AppDbContext _dbContext;

    public LandedCostVoucherRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Join an already-open transaction instead of creating a nested one (same DbContext instance).
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task AddAsync(LandedCostVoucher voucher, CancellationToken cancellationToken = default)
    {
        await _dbContext.LandedCostVouchers.AddAsync(voucher, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<LandedCostVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.LandedCostVouchers
            .Include(v => v.Receipts)
            .Include(v => v.Charges)
            .Include(v => v.Items)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<PagedResult<LandedCostVoucher>> GetByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        DocumentStatus? status,
        CancellationToken cancellationToken = default)
        => await _dbContext.LandedCostVouchers
            .Where(v => v.CompanyId == companyId
                && (!status.HasValue || v.Status == status.Value))
            .Include(v => v.Receipts)
            .Include(v => v.Charges)
            .Include(v => v.Items)
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .ToPagedResultAsync(paging, cancellationToken);

    public async Task UpdateAsync(LandedCostVoucher voucher, CancellationToken cancellationToken = default)
    {
        _dbContext.LandedCostVouchers.Update(voucher);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
