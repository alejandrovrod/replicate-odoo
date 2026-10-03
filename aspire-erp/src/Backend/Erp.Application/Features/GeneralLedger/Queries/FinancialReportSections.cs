using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Shared renderer of a statement section (tasks.md 2.5): turns the raw per-account ledger sums
/// into the pinned <c>{ rows[], total }</c> shape both the balance sheet and the profit &amp; loss
/// use, applying the section's sign convention and ordering rows by AccountCode.
/// </summary>
/// <remarks>
/// The sign is the ONLY thing that differs between sections: debit-natured sections
/// (Asset, COGS, Expense) show <c>Debit − Credit</c>, credit-natured sections (Liability, Equity,
/// Income) show <c>Credit − Debit</c>. Presenting every section in its natural sign is what lets a
/// reader add the sections up - and what makes the balance sheet's <c>assets − (liabilities +
/// equity)</c> equal the P&amp;L's <c>netProfit</c> for the same period.
/// </remarks>
internal static class FinancialReportSections
{
    /// <summary>
    /// Builds one section from the accounts whose aggregates the caller selected.
    /// </summary>
    /// <param name="balances">Already-filtered account aggregates (section membership).</param>
    /// <param name="creditNatured">
    /// True for a credit-natured section (net = Credit − Debit); false for a debit-natured one
    /// (net = Debit − Credit).
    /// </param>
    public static FinancialSectionDto Build(
        IEnumerable<AccountBalanceRow> balances,
        bool creditNatured)
    {
        var rows = new List<FinancialSectionRowDto>();
        decimal total = 0m;

        foreach (var balance in balances.OrderBy(b => b.AccountCode, StringComparer.Ordinal))
        {
            var net = creditNatured
                ? balance.TotalCredit - balance.TotalDebit
                : balance.TotalDebit - balance.TotalCredit;

            rows.Add(new FinancialSectionRowDto(
                balance.AccountId,
                balance.AccountCode,
                balance.AccountName,
                net));
            total += net;
        }

        return new FinancialSectionDto(rows, total);
    }
}
