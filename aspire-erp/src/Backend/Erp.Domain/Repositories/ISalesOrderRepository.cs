using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the sales order workflow (Task 5.2): Draft creation with the gapless
/// order number (Constitution III.4), status transitions and the reads the delivery posting needs.
/// Implemented by Erp.Infrastructure.Data.Repositories.SalesRepository.
/// </summary>
/// <remarks>
/// Creation and submission run inside <see cref="ExecuteInTransactionAsync{T}"/> so the order
/// number, the header and its lines commit atomically - the same contract as
/// <see cref="IPurchaseRepository"/>. Note the number is drawn from the <c>OrderNumber</c> column
/// (per plan.md §1), not a VoucherNo column.
/// </remarks>
public interface ISalesOrderRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole operation back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking (same contract as IStockRepository).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Next gapless sales-order number for the company/year, e.g. "SO-2026-00001" (ambient transaction required).</summary>
    Task<string> NextOrderNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    /// <summary>Persists a Draft sales order with its lines (inside the ambient creation transaction).</summary>
    Task AddOrderAsync(SalesOrder order, CancellationToken cancellationToken = default);

    /// <summary>Saves workflow and delivery progress of an already-tracked order (inside the ambient transaction).</summary>
    Task UpdateOrderAsync(SalesOrder order, CancellationToken cancellationToken = default);

    /// <summary>The order with its lines, or null when it does not exist in this tenant.</summary>
    Task<SalesOrder?> GetOrderByIdAsync(Guid salesOrderId, CancellationToken cancellationToken = default);

    /// <summary>Most recent sales orders of a company (newest first) with lines.</summary>
    Task<PagedResult<SalesOrder>> GetRecentOrdersByCompanyAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default);
}
