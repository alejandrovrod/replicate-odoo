using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// Trial balance payload of <c>GET /api/v1/FinancialReports/trial-balance</c>: one row per account
/// that MOVED on or before <see cref="AsOfDate"/>, plus the two column totals and their
/// discrepancy - the acceptance of tasks.md 2.5 ("Trial balance reports zero discrepancy").
/// </summary>
/// <remarks>
/// Semantics are plan.md §4's canonical SQL: <c>SUM(Debit)</c>/<c>SUM(Credit)</c> per account over
/// <c>PostingDate &lt;= AsOfDate</c>, joined to Account for the display columns, keeping only
/// accounts with movement. <see cref="NetBalance"/> is the TECHNICAL net (Debit − Credit) for
/// EVERY account - a trial balance is an audit worksheet with Dr/Cr columns, not a financial
/// statement, so credit balances legitimately appear negative here (the balance sheet applies the
/// natural sign instead). <see cref="Difference"/> over balanced data is exactly 0.0000
/// (Constitution III.1 zero-sum).
/// </remarks>
/// <param name="AsOfDate">Cutoff date of the statement, echoed back (serialized as <c>yyyy-MM-dd</c>).</param>
/// <param name="Rows">Account aggregates, ordered by AccountCode.</param>
/// <param name="TotalDebit">SUM(TotalDebit) over every row.</param>
/// <param name="TotalCredit">SUM(TotalCredit) over every row.</param>
/// <param name="Difference">totalDebit − totalCredit (0.0000 on a balanced ledger).</param>
public sealed record TrialBalanceReportDto(
    DateOnly AsOfDate,
    IReadOnlyList<TrialBalanceRowDto> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference);

/// <summary>One account line of the trial balance.</summary>
/// <param name="AccountId">Posting account the aggregates belong to.</param>
/// <param name="AccountCode">Display code, e.g. "1110".</param>
/// <param name="AccountName">Display name.</param>
/// <param name="RootType">Asset / Liability / Equity / Income / Expense (serialized as its name).</param>
/// <param name="TotalDebit">SUM(Debit) up to the statement date.</param>
/// <param name="TotalCredit">SUM(Credit) up to the statement date.</param>
/// <param name="NetBalance">TotalDebit − TotalCredit (technical net, negative on credit balances).</param>
public sealed record TrialBalanceRowDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    AccountRootType RootType,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetBalance);
