using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetBalanceSheetQuery"/> from <see cref="IGLEntryRepository"/>: the ledger
/// nets per account, each root type becomes one section in its natural sign, and the
/// accounting-equation check decides <see cref="BalanceSheetReportDto.Balanced"/>.
/// </summary>
public sealed class GetBalanceSheetQueryHandler
    : IQueryHandler<GetBalanceSheetQuery, BalanceSheetReportDto>
{
    /// <summary>
    /// Tolerance of the <c>Assets = Liabilities + Equity</c> check. Deliberately the SAME
    /// 0.0001 the posting engine uses for <c>double_entry_imbalance</c>, so the statement and the
    /// invariant that produced it agree on what "balanced" means down to the last 4 decimals.
    /// </summary>
    public const decimal BalanceTolerance = 0.0001m;

    private readonly IGLEntryRepository _ledger;

    public GetBalanceSheetQueryHandler(IGLEntryRepository ledger)
    {
        _ledger = ledger;
    }

    public async Task<BalanceSheetReportDto> HandleAsync(
        GetBalanceSheetQuery query,
        CancellationToken cancellationToken = default)
    {
        var balances = await _ledger.GetAccountBalancesAsync(
            query.CompanyId,
            from: null,
            to: query.AsOfDate,
            cancellationToken);

        // Debit-natured (Asset) vs credit-natured (Liability/Equity) presentation - what turns the
        // raw ledger sums into statements a reader can add up across sections.
        var assets = FinancialReportSections.Build(
            balances.Where(b => b.RootType == AccountRootType.Asset),
            creditNatured: false);
        var liabilities = FinancialReportSections.Build(
            balances.Where(b => b.RootType == AccountRootType.Liability),
            creditNatured: true);
        var equity = FinancialReportSections.Build(
            balances.Where(b => b.RootType == AccountRootType.Equity),
            creditNatured: true);

        // Income/Expense are excluded on purpose (they are the P&L), so the residual of this
        // equation equals the period's unclosed net profit - i.e. the statement reports "false"
        // until the books are closed, never by accident.
        var balanced = Math.Abs(assets.Total - (liabilities.Total + equity.Total)) <= BalanceTolerance;

        return new BalanceSheetReportDto(assets, liabilities, equity, balanced);
    }
}
