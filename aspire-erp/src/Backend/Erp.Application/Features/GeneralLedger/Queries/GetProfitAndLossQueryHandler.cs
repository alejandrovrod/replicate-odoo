using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetProfitAndLossQuery"/> from <see cref="IGLEntryRepository"/>: nets the
/// ledger over the period, splits the Expense root into COGS and operating expenses by
/// <c>Account.Type</c>, and subtracts both from revenue to get the result of the period.
/// </summary>
public sealed class GetProfitAndLossQueryHandler
    : IQueryHandler<GetProfitAndLossQuery, ProfitAndLossReportDto>
{
    private readonly IGLEntryRepository _ledger;

    public GetProfitAndLossQueryHandler(IGLEntryRepository ledger)
    {
        _ledger = ledger;
    }

    public async Task<ProfitAndLossReportDto> HandleAsync(
        GetProfitAndLossQuery query,
        CancellationToken cancellationToken = default)
    {
        var balances = await _ledger.GetAccountBalancesAsync(
            query.CompanyId,
            from: query.From,
            to: query.To,
            cancellationToken);

        var revenue = FinancialReportSections.Build(
            balances.Where(b => b.RootType == AccountRootType.Income),
            creditNatured: true);

        // The Expense root splits by the ERPNext account_type: COGS is the cost of what was sold,
        // every other expense account is operating - together they cover the root exactly once.
        var cogs = FinancialReportSections.Build(
            balances.Where(b => b.RootType == AccountRootType.Expense && b.Type == AccountType.COGS),
            creditNatured: false);
        var expenses = FinancialReportSections.Build(
            balances.Where(b => b.RootType == AccountRootType.Expense && b.Type != AccountType.COGS),
            creditNatured: false);

        return new ProfitAndLossReportDto(
            revenue,
            cogs,
            expenses,
            revenue.Total - cogs.Total - expenses.Total);
    }
}
