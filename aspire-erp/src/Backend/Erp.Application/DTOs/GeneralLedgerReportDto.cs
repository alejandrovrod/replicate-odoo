using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// One immutable General Ledger row as returned by
/// <c>GET /api/v1/FinancialReports/general-ledger</c> - the PINNED Task 2.6 contract consumed by
/// the React audit viewer (<c>GeneralLedgerOverview.tsx</c>), so field names and shapes are frozen.
/// </summary>
/// <remarks>
/// The row is the raw forensic record (Constitution III.2/IV.2): amounts in
/// <see cref="AccountCurrency"/>, the source voucher that produced it, the counterparty and cost
/// centre dimensions, plus <see cref="IsCancelled"/> to tell an original line from a compensating
/// reversal. Cancellation never rewrites a row - it appends swapped counter-lines - so BOTH kinds
/// are reported and the client sees the full history of a voucher.
/// </remarks>
/// <param name="Id">BIGINT identity of the ledger row - unique across the ledger, the row's stable key.</param>
/// <param name="PostingDate">Accounting date of the posting (serialized as <c>yyyy-MM-dd</c>).</param>
/// <param name="AccountId">Posting (leaf) account that received the line.</param>
/// <param name="AccountCode">Display code of that account, e.g. "1110".</param>
/// <param name="AccountName">Display name of that account.</param>
/// <param name="RootType">AccountRootType of the account, serialized as its name ("Asset", ...).</param>
/// <param name="Debit">Debit amount (decimal(18,4), 0.0000 on a credit-only line).</param>
/// <param name="Credit">Credit amount (decimal(18,4), 0.0000 on a debit-only line).</param>
/// <param name="AccountCurrency">ISO 4217 snapshot currency of the account at posting time.</param>
/// <param name="VoucherType">Source document type, e.g. "JournalEntry".</param>
/// <param name="VoucherNo">Source document number, e.g. "JV-2026-00001".</param>
/// <param name="VoucherId">Primary key of the source document - the drill-down key.</param>
/// <param name="PartyType">Party role of the counter-occurrence, or null when there is none.</param>
/// <param name="PartyId">Primary key of the referenced party, or null.</param>
/// <param name="CostCenterId">Cost centre dimension, or null until the module posts one.</param>
/// <param name="IsCancelled">
/// True when this row IS a compensating reversal of a cancelled voucher (the marker travels on the
/// reversal rows, never on the originals).
/// </param>
/// <param name="Remarks">Free-text remark of the posting, or null.</param>
/// <param name="CreatedAt">UTC timestamp of when the row was appended (ISO-8601 on the wire).</param>
public sealed record GeneralLedgerEntryDto(
    long Id,
    DateOnly PostingDate,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    AccountRootType RootType,
    decimal Debit,
    decimal Credit,
    string AccountCurrency,
    string VoucherType,
    string VoucherNo,
    Guid VoucherId,
    string? PartyType,
    Guid? PartyId,
    Guid? CostCenterId,
    bool IsCancelled,
    string? Remarks,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Maps one ledger row with its posting account (loaded by the repository through the NOT NULL
    /// FK_GLEntry_Account). The guard fails loudly instead of rendering a report row with a made-up
    /// account: a ledger line without an account is a broken invariant, not a display glitch.
    /// </summary>
    /// <param name="entry">Ledger row whose <c>Account</c> navigation is populated.</param>
    public static GeneralLedgerEntryDto Build(GLEntry entry)
    {
        var account = entry.Account
            ?? throw new InvalidOperationException(
                $"GLEntry {entry.Id} arrived without its posting account. "
                + "GetGeneralLedgerPageAsync must Include(GLEntry.Account): FK_GLEntry_Account is "
                + "NOT NULL, so a report row can never invent the account it belongs to.");

        return new GeneralLedgerEntryDto(
            entry.Id,
            entry.PostingDate,
            entry.AccountId,
            account.AccountCode,
            account.AccountName,
            account.RootType,
            entry.Debit,
            entry.Credit,
            entry.AccountCurrency,
            entry.VoucherType,
            entry.VoucherNo,
            entry.VoucherId,
            entry.PartyType,
            entry.PartyId,
            entry.CostCenterId,
            entry.IsCancelled,
            entry.Remarks,
            entry.CreatedAt);
    }
}

/// <summary>
/// General ledger report payload of <c>GET /api/v1/FinancialReports/general-ledger</c> (pinned
/// Task 2.6 contract): a chronological page of rows plus the totals of the FULL filtered set.
/// </summary>
/// <remarks>
/// <see cref="Difference"/> is <c>totalDebit − totalCredit</c>, computed SERVER-SIDE over every
/// matching row - deliberately NOT recomputed from <see cref="Items"/>, because <c>take</c>
/// (default 500, max 5000) may truncate the page while the totals must still describe the whole
/// filter. A balanced ledger reports exactly 0.0000, which is the badge the audit viewer renders.
/// </remarks>
/// <param name="Items">Rows of this page, ordered by PostingDate ASC then Id ASC.</param>
/// <param name="TotalDebit">SUM(Debit) over the full filtered set.</param>
/// <param name="TotalCredit">SUM(Credit) over the full filtered set.</param>
/// <param name="Difference">totalDebit − totalCredit (0.0000 on a balanced ledger).</param>
public sealed record GeneralLedgerReportDto(
    IReadOnlyList<GeneralLedgerEntryDto> Items,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference);
