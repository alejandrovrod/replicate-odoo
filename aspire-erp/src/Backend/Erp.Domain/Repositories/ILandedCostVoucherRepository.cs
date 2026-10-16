using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for landed cost vouchers (Fase 4). Implemented by
/// Erp.Infrastructure. Tenant isolation is AUTOMATIC (Constitution II.3).
/// </summary>
public interface ILandedCostVoucherRepository
{
    /// <summary>Persists a new Draft voucher with references, charges and apportionment.</summary>
    Task AddAsync(LandedCostVoucher voucher, CancellationToken cancellationToken = default);

    /// <summary>The voucher with the given id (references/charges/items included), or null.</summary>
    Task<LandedCostVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Company vouchers, newest first (optional status filter).</summary>
    Task<PagedResult<LandedCostVoucher>> GetByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        DocumentStatus? status,
        CancellationToken cancellationToken = default);

    /// <summary>Persists voucher mutations (submit/cancel transitions).</summary>
    Task UpdateAsync(LandedCostVoucher voucher, CancellationToken cancellationToken = default);

    /// <summary>Runs the operation inside ONE database transaction (posting atomicity).</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
