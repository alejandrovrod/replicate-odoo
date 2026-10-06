using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetJournalEntriesQuery"/> from <see cref="IJournalRepository"/>
/// (header + lines + line accounts in one round trip). Tenant isolation is automatic
/// (Constitution II.3).
/// </summary>
public sealed class GetJournalEntriesQueryHandler
    : IQueryHandler<GetJournalEntriesQuery, PagedResult<JournalEntryDto>>
{
    private readonly IJournalRepository _journals;

    public GetJournalEntriesQueryHandler(IJournalRepository journals)
    {
        _journals = journals;
    }

    public async Task<PagedResult<JournalEntryDto>> HandleAsync(
        GetJournalEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _journals.GetRecentByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<JournalEntryDto>());
        }

        var result = new List<JournalEntryDto>(page.Items.Count);
        foreach (var entry in page.Items)
        {
            result.Add(JournalEntryDto.Build(entry));
        }

        return page.Map(result);
    }
}
