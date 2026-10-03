namespace Erp.Application.DTOs;

/// <summary>
/// Profit &amp; loss payload of <c>GET /api/v1/FinancialReports/profit-and-loss</c> (pinned Task
/// 2.5 contract): revenue, cost of goods sold, operating expenses and the resulting net profit
/// (tasks.md 2.5: "Revenue - COGS - Expenses = Net Profit").
/// </summary>
/// <remarks>
/// <para><b>Section selectors.</b> <see cref="Revenue"/> is RootType Income; the Expense root is
/// split by <c>Account.Type</c>: accounts typed <c>COGS</c> land in <see cref="Cogs"/> and every
/// other expense account in <see cref="Expenses"/> - ERPNext's own revenue-recognition split, so
/// gross profit (revenue − cogs) stays readable.</para>
/// <para><b>Signs.</b> Revenue is presented credit-positive (Credit − Debit) and both expense
/// sections debit-positive (Debit − Credit), which is exactly what makes
/// <c>netProfit = revenue − cogs − expenses</c> an ordinary subtraction that yields a POSITIVE
/// profit and a NEGATIVE result for a loss-making period.</para>
/// </remarks>
/// <param name="Revenue">RootType Income accounts, balances as Credit − Debit.</param>
/// <param name="Cogs">RootType Expense accounts with Type == COGS, balances as Debit − Credit.</param>
/// <param name="Expenses">RootType Expense accounts with Type != COGS, balances as Debit − Credit.</param>
/// <param name="NetProfit">revenue.Total − cogs.Total − expenses.Total (negative = loss).</param>
public sealed record ProfitAndLossReportDto(
    FinancialSectionDto Revenue,
    FinancialSectionDto Cogs,
    FinancialSectionDto Expenses,
    decimal NetProfit);
