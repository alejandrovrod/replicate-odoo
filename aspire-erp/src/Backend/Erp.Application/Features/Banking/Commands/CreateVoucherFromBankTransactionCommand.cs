using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Creates a balanced SUBMITTED <see cref="Domain.Entities.JournalEntry"/> from one unmatched
/// staging line and reconciles the line against it, atomically (task 6.5, scenario BN-04).
/// </summary>
/// <remarks>
/// <para><b>Boundary with Block B (task 6.3).</b> The rules engine only REPORTS
/// <c>RequiresVoucherCreation</c> and posts nothing; automatic invocation stays OUT - the
/// operator confirms the account and amount in the dialog, and THIS command does the posting.
/// Nothing in the Block B files is retro-fitted: this command is purely additive.</para>
/// <para><b>Direction.</b> A withdrawal (money out, e.g. a $15 bank fee) posts
/// Dr expense / Cr bank; a deposit (money in, e.g. interest) posts the mirror -
/// Dr bank / Cr <c>ExpenseAccountCode</c> - so both sides stay balanced by construction.</para>
/// </remarks>
/// <param name="CompanyId">Company that owns the transaction and both ledger accounts.</param>
/// <param name="BankTransactionId">Unreconciled or Matched staging line to voucher and reconcile.</param>
/// <param name="ExpenseAccountCode">Active leaf account code (e.g. "5150").</param>
/// <param name="Amount">Optional explicit amount; when given it must equal |Deposit - Withdrawal| exactly.</param>
/// <param name="Memo">Optional voucher remark; defaults to a reference to the statement narrative.</param>
/// <param name="RowVersion">Optional optimistic token - a stale token fails fast.</param>
public sealed record CreateVoucherFromBankTransactionCommand(
    Guid CompanyId,
    Guid BankTransactionId,
    string ExpenseAccountCode,
    decimal? Amount = null,
    string? Memo = null,
    byte[]? RowVersion = null) : ICommand<Result<JournalEntryDto>>;
