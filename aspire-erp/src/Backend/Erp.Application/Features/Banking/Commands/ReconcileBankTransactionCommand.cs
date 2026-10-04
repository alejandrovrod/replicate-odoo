using Erp.Application.Common;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// One allocation slice of a reconciliation request: exactly one of
/// <see cref="PaymentEntryId"/> / <see cref="GlVoucherId"/> must be set.
/// </summary>
/// <param name="PaymentEntryId">Internal payment voucher to allocate against, if any.</param>
/// <param name="GlVoucherId">
/// Posted GL voucher (<c>GLEntry.VoucherId</c>, e.g. a JournalEntry id) to allocate against,
/// if any.
/// </param>
/// <param name="Amount">Slice amount (strictly positive).</param>
public sealed record ReconciliationLine(
    Guid? PaymentEntryId,
    Guid? GlVoucherId,
    decimal Amount);

/// <summary>
/// Reconciles one staging transaction against payment vouchers and/or posted GL vouchers
/// (task 6.4, scenarios BN-03/BN-04/BN-07).
/// </summary>
/// <param name="CompanyId">Company that owns the transaction and every counterpart.</param>
/// <param name="BankTransactionId">Staging line to reconcile.</param>
/// <param name="Lines">Allocation slices; their sum must equal |Deposit - Withdrawal| exactly.</param>
/// <param name="RowVersion">Optional optimistic token - a stale token fails fast.</param>
public sealed record ReconcileBankTransactionCommand(
    Guid CompanyId,
    Guid BankTransactionId,
    IReadOnlyList<ReconciliationLine> Lines,
    byte[]? RowVersion = null) : ICommand<Result<ReconciliationSummary>>;

/// <summary>Reconciliation outcome: the reconciled line plus its persisted slices.</summary>
/// <param name="BankTransactionId">Staging line now Reconciled.</param>
/// <param name="AllocatedAmount">Sum of the slices (= |Deposit - Withdrawal|).</param>
/// <param name="ClearanceDate">Stamped clearance (= the line's TransactionDate, BN-03).</param>
/// <param name="Lines">Persisted slices with their link ids.</param>
public sealed record ReconciliationSummary(
    Guid BankTransactionId,
    decimal AllocatedAmount,
    DateOnly ClearanceDate,
    IReadOnlyList<ReconciledLine> Lines);

/// <summary>One persisted allocation slice.</summary>
public sealed record ReconciledLine(
    Guid LinkId,
    string CounterpartType,
    Guid CounterpartId,
    decimal Amount);
