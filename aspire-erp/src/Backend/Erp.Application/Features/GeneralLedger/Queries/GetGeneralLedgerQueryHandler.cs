using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetGeneralLedgerQuery"/> from <see cref="IGLEntryRepository"/>: one
/// repository call returns the chronological page AND the Debit/Credit totals of the full
/// filtered set, so the report can never show page-local totals (pinned Task 2.6 contract - the
/// audit viewer's footer badge reads them while <c>take</c> truncates the visible rows).
/// </summary>
public sealed class GetGeneralLedgerQueryHandler
    : IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerReportDto>
{
    private readonly IGLEntryRepository _ledger;

    public GetGeneralLedgerQueryHandler(IGLEntryRepository ledger)
    {
        _ledger = ledger;
    }

    public async Task<GeneralLedgerReportDto> HandleAsync(
        GetGeneralLedgerQuery query,
        CancellationToken cancellationToken = default)
    {
        // take <= 0 means "the client did not ask", never "give me nothing": an unbounded or
        // nonsensical page size degrades to the documented default instead of an error.
        var take = query.Take <= 0
            ? GeneralLedgerPaging.DefaultTake
            : Math.Min(query.Take, GeneralLedgerPaging.MaxTake);

        var page = await _ledger.GetGeneralLedgerPageAsync(
            query.CompanyId,
            new GeneralLedgerFilter(
                query.AccountId,
                query.VoucherId,
                query.VoucherType,
                query.From,
                query.To),
            take,
            cancellationToken);

        var items = new List<GeneralLedgerEntryDto>(page.Items.Count);
        foreach (var entry in page.Items)
        {
            items.Add(GeneralLedgerEntryDto.Build(entry));
        }

        // The difference is a statement about the WHOLE filtered set, so it is derived from the
        // repository's full-set totals - never from the (possibly truncated) item list.
        return new GeneralLedgerReportDto(
            items,
            page.TotalDebit,
            page.TotalCredit,
            page.TotalDebit - page.TotalCredit);
    }
}
