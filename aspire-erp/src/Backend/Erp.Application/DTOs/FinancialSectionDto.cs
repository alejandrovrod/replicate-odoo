namespace Erp.Application.DTOs;

/// <summary>
/// One account line of a balance-sheet or profit-and-loss section. Both statements share this
/// shape on purpose (pinned Task 2.5 contract): a section is always "the account rows plus their
/// total", only the accounts it selects and the SIGN of <see cref="Balance"/> change per report.
/// </summary>
/// <param name="AccountId">Posting account the balance belongs to.</param>
/// <param name="AccountCode">Display code, e.g. "1110".</param>
/// <param name="AccountName">Display name.</param>
/// <param name="Balance">
/// Signed balance of the account inside the statement period, already presented in the natural
/// sign of its section (debits positive on Asset/Expense, credits positive on
/// Liability/Equity/Income).
/// </param>
public sealed record FinancialSectionRowDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Balance);

/// <summary>
/// A statement section: the account rows (ordered by AccountCode) and their summed total. Used by
/// <see cref="BalanceSheetReportDto"/> (assets / liabilities / equity) and by
/// <see cref="ProfitAndLossReportDto"/> (revenue / cogs / expenses).
/// </summary>
/// <param name="Rows">Accounts with movement in this section.</param>
/// <param name="Total">SUM(Balance) over <paramref name="Rows"/>.</param>
public sealed record FinancialSectionDto(
    IReadOnlyList<FinancialSectionRowDto> Rows,
    decimal Total);
