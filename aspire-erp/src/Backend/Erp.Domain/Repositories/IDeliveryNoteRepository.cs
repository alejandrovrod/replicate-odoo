using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for delivery notes (Task 5.2b / Amendment A1): the gapless DN voucher
/// generator (Constitution III.4), persistence of a posted delivery and its reads.
/// Implemented by Erp.Infrastructure.Data.Repositories.SalesRepository.
/// </summary>
/// <remarks>
/// Posting runs inside <see cref="ExecuteInTransactionAsync{T}"/> together with the stock ledger,
/// the GL and the sales-order update, so the whole delivery commits or rolls back as one unit
/// (same contract as <see cref="IPurchaseRepository"/>).
/// </remarks>
public interface IDeliveryNoteRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole posting back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking (same contract as IStockRepository).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Next gapless delivery-note number for the company/year, e.g. "DN-2026-00001" (ambient transaction required).</summary>
    Task<string> NextDeliveryVoucherNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    /// <summary>Persists a posted delivery note with its lines (inside the ambient posting transaction).</summary>
    Task AddDeliveryNoteAsync(DeliveryNote deliveryNote, CancellationToken cancellationToken = default);

    /// <summary>The delivery note with its lines, or null when it does not exist in this tenant.</summary>
    Task<DeliveryNote?> GetDeliveryNoteByIdAsync(Guid deliveryNoteId, CancellationToken cancellationToken = default);

    /// <summary>Most recent delivery notes of a company (newest first) with lines.</summary>
    Task<IReadOnlyList<DeliveryNote>> GetRecentDeliveryNotesByCompanyAsync(Guid companyId, int limit, CancellationToken cancellationToken = default);
}
