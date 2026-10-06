using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the Journal Entry voucher (tasks.md 2.3): draft persistence, the
/// workflow transitions, the JV gapless voucher generator (Constitution III.4), the General
/// Ledger appends produced by submit/cancel and the reads the API needs. Implemented by
/// Erp.Infrastructure.Data.Repositories.JournalRepository.
/// </summary>
/// <remarks>
/// All writes happen through <see cref="ExecuteInTransactionAsync{T}"/> so the voucher number, the
/// header status and the GLEntry rows commit atomically - the acceptance of tasks.md 2.4
/// ("updates ledger balances atomically") is exactly this contract: a submit that fails anywhere
/// (imbalance, freeze, group account, concurrency) rolls back with ZERO ledger rows
/// (spec AC-02/AC-04).
/// </remarks>
public interface IJournalRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole posting back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking (same contract as IStockRepository/IPurchaseRepository).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Next gapless journal number, e.g. prefix "JV" + 2026 -&gt; "JV-2026-00001"
    /// (ambient transaction required - Constitution III.4).
    /// </summary>
    Task<string> NextVoucherNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>Persists a Draft journal entry with its lines (inside the ambient creation transaction).</summary>
    Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the workflow transition of an already-tracked entry (inside the ambient transaction).
    /// Translates EF's <c>DbUpdateConcurrencyException</c> (RowVersion WHERE clause matched 0 rows)
    /// into <c>ConcurrencyConflictException</c> - the typed failure the handler turns into a
    /// <c>concurrency_conflict</c> 409.
    /// </summary>
    Task UpdateAsync(JournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Persists the General Ledger lines produced by submit/cancel (inside the ambient posting transaction).</summary>
    Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default);

    /// <summary>The entry with its lines and line accounts, or null when it does not exist in this tenant.</summary>
    Task<JournalEntry?> GetByIdAsync(Guid journalEntryId, CancellationToken cancellationToken = default);

    /// <summary>Most recent journal entries of a company (newest first) with lines.</summary>
    Task<PagedResult<JournalEntry>> GetRecentByCompanyAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default);
}
