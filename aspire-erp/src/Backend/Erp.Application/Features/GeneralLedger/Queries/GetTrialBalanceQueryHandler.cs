using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetTrialBalanceQuery"/> from <see cref="IGLEntryRepository"/>: per-account
/// <c>SUM(Debit)</c>/<c>SUM(Credit)</c> up to the cutoff (plan.md §4), rendered as the technical
/// Dr/Cr worksheet every accountant checks before trusting the statements.
/// </summary>
/// <remarks>
/// <see cref="TrialBalanceReportDto.Difference"/> accumulates the SAME numbers the rows show, so
/// "totalDebit − totalCredit == 0.0000" proves the ledger itself is zero-sum (Constitution III.1
/// / spec AC-01) - the acceptance the integration test asserts against the live container.
/// </remarks>
public sealed class GetTrialBalanceQueryHandler
    : IQueryHandler<GetTrialBalanceQuery, TrialBalanceReportDto>
{
    private readonly IGLEntryRepository _ledger;

    public GetTrialBalanceQueryHandler(IGLEntryRepository ledger)
    {
        _ledger = ledger;
    }

    public async Task<TrialBalanceReportDto> HandleAsync(
        GetTrialBalanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var balances = await _ledger.GetAccountBalancesAsync(
            query.CompanyId,
            from: null,
            to: query.AsOfDate,
            cancellationToken);

        var rows = new List<TrialBalanceRowDto>(balances.Count);
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var balance in balances.OrderBy(b => b.AccountCode, StringComparer.Ordinal))
        {
            // "Only accounts with movement" (plan.md §4 joins GLEntry, so an untouched account
            // has no row). The guard also drops a hypothetical 0.0000/0.0000 line, which carries
            // no information for the reader.
            if (balance.TotalDebit == 0m && balance.TotalCredit == 0m)
            {
                continue;
            }

            rows.Add(new TrialBalanceRowDto(
                balance.AccountId,
                balance.AccountCode,
                balance.AccountName,
                balance.RootType,
                balance.TotalDebit,
                balance.TotalCredit,
                balance.TotalDebit - balance.TotalCredit));

            totalDebit += balance.TotalDebit;
            totalCredit += balance.TotalCredit;
        }

        return new TrialBalanceReportDto(
            query.AsOfDate,
            rows,
            totalDebit,
            totalCredit,
            totalDebit - totalCredit);
    }
}
