using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetJournalEntriesQuery"/> from <see cref="IJournalRepository"/>
/// (header + lines + line accounts in one round trip). Tenant isolation is automatic
/// (Constitution II.3).
/// </summary>
public sealed class GetJournalEntriesQueryHandler
    : IQueryHandler<GetJournalEntriesQuery, IReadOnlyList<JournalEntryDto>>
{
    private readonly IJournalRepository _journals;

    public GetJournalEntriesQueryHandler(IJournalRepository journals)
    {
        _journals = journals;
    }

    public async Task<IReadOnlyList<JournalEntryDto>> HandleAsync(
        GetJournalEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var entries = await _journals.GetRecentByCompanyAsync(query.CompanyId, limit, cancellationToken);
        if (entries.Count == 0)
        {
            return Array.Empty<JournalEntryDto>();
        }

        var result = new List<JournalEntryDto>(entries.Count);
        foreach (var entry in entries)
        {
            result.Add(JournalEntryDto.Build(entry));
        }

        return result;
    }
}
