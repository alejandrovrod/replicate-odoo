using System.Data;
using Erp.Domain.Entities;
using Erp.Domain.Services;

namespace Erp.Domain.Repositories;

/// <summary>
/// One FY-windowed P&amp;L leaf balance (credit-normal), shared by the submit pipeline and the
/// read-only preview (plan.md §4: preview and submit share the single source of truth).
/// </summary>
public sealed record UnclosedPLBalance(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    AccountRootType RootType,
    decimal Balance);

/// <summary>
/// Data-access contract for the <see cref="PeriodClosingVoucher"/> aggregate (R-13, rewritten per
/// spec §7 audit). Reads are FY-windowed (plan.md §4 query); cancellation is reversal-append
/// only — the mutating <c>UpdateGLEntries</c> path is DELETED (spec FC-06, Constitution III.2).
/// </summary>
public interface IPeriodClosingVoucherRepository
{
    Task<PeriodClosingVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PeriodClosingVoucher?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task AddAsync(PeriodClosingVoucher voucher, CancellationToken cancellationToken = default);

    void Update(PeriodClosingVoucher voucher);

    Task<List<PeriodClosingVoucher>> GetPagedAsync(Guid companyId, Guid? fiscalYearId, DocumentStatus? status, int skip, int take, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializable variant for submit/cancel: re-reads the voucher, the duplicate-year guard and
    /// the balances inside the txn (plan.md §6, spec FC-10 race).
    /// </summary>
    Task<T> ExecuteInSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// FY-windowed P&amp;L balances (plan.md §4): credit-normal balances of active non-group
    /// Income/Expense leaves of the company with <c>PostingDate ∈ [StartDate, EndDate]</c>,
    /// non-cancelled rows only, excluding rows produced by an already-submitted close of the SAME
    /// year (fixpoint: post-close re-query returns empty). Balance Sheet leaves never returned.
    /// </summary>
    Task<List<UnclosedPLBalance>> GetUnclosedPLBalancesAsync(Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);

    /// <summary>True when a submitted non-cancelled voucher exists for the year (spec FC-05).</summary>
    Task<bool> HasSubmittedCloseAsync(Guid companyId, Guid fiscalYearId, Guid? excludeVoucherId = null, CancellationToken cancellationToken = default);

    /// <summary>True when any Draft voucher remains for the year (blocks CloseFiscalYear, FC-14).</summary>
    Task<bool> HasDraftVoucherAsync(Guid fiscalYearId, CancellationToken cancellationToken = default);

    /// <summary><c>VoucherNo</c>s of submitted closes of the year (fixpoint exclusion set).</summary>
    Task<List<string>> GetVoucherNosOfYearAsync(Guid fiscalYearId, CancellationToken cancellationToken = default);

    /// <summary>Gapless-per-year voucher number <c>PCV-&lt;yyyy&gt;-&lt;seq&gt;</c> born inside the submit txn.</summary>
    Task<string> NextClosingVoucherNumberAsync(Guid companyId, FiscalYear fiscalYear, CancellationToken cancellationToken = default);

    Task AddGLEntriesAsync(IEnumerable<GLEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>Every GL row of one closing voucher (originals + reversals, byte-identical originals).</summary>
    Task<List<GLEntry>> GetGLEntriesByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default);

    Task AddLinesAsync(IEnumerable<PeriodClosingVoucherLine> lines, CancellationToken cancellationToken = default);
}
