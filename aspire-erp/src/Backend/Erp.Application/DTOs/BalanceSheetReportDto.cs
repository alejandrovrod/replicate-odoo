namespace Erp.Application.DTOs;

/// <summary>
/// Balance sheet payload of <c>GET /api/v1/FinancialReports/balance-sheet</c> (pinned Task 2.5
/// contract): the three accounting-equation sections and whether they balance.
/// </summary>
/// <remarks>
/// <para><b>Scope.</b> Only RootType Asset / Liability / Equity reach this report - Income and
/// Expense are deliberately EXCLUDED (they belong to the P&amp;L), so an open period makes
/// <see cref="Balanced"/> false by exactly the period's net profit, which is the textbook
/// <c>Assets = Liabilities + Equity + Retained Earnings</c> equation with the earnings not yet
/// closed into equity.</para>
/// <para><b>Sign convention.</b> Each section presents its accounts in their NATURAL sign
/// (Asset = Debit − Credit, Liability/Equity = Credit − Debit), which is what makes
/// <see cref="Balanced"/> a meaningful test: |assets − (liabilities + equity)| ≤ 0.0001, the same
/// 0.0001 tolerance the posting engine uses for the double-entry invariant.</para>
/// </remarks>
/// <param name="Assets">RootType Asset accounts, balances as Debit − Credit.</param>
/// <param name="Liabilities">RootType Liability accounts, balances as Credit − Debit.</param>
/// <param name="Equity">RootType Equity accounts, balances as Credit − Debit.</param>
/// <param name="Balanced">True when |assets − (liabilities + equity)| ≤ 0.0001.</param>
public sealed record BalanceSheetReportDto(
    FinancialSectionDto Assets,
    FinancialSectionDto Liabilities,
    FinancialSectionDto Equity,
    bool Balanced);
