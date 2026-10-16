using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ILandedCostVoucherRepository"/> over a seeded voucher set.</summary>
public sealed class FakeLandedCostVoucherRepository : ILandedCostVoucherRepository
{
    private readonly List<LandedCostVoucher> _vouchers = new();

    public IReadOnlyList<LandedCostVoucher> Vouchers => _vouchers;

    public void Seed(params LandedCostVoucher[] vouchers) => _vouchers.AddRange(vouchers);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        => operation(cancellationToken);

    public Task AddAsync(LandedCostVoucher voucher, CancellationToken cancellationToken = default)
    {
        _vouchers.Add(voucher);
        return Task.CompletedTask;
    }

    public Task<LandedCostVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_vouchers.FirstOrDefault(v => v.Id == id));

    public Task<PagedResult<LandedCostVoucher>> GetByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        DocumentStatus? status,
        CancellationToken cancellationToken = default)
    {
        var items = _vouchers
            .Where(v => v.CompanyId == companyId && (!status.HasValue || v.Status == status.Value))
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .ToList();
        return Task.FromResult(new PagedResult<LandedCostVoucher>(
            items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task UpdateAsync(LandedCostVoucher voucher, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
