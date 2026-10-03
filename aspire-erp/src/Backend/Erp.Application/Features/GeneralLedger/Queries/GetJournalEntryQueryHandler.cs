using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>Single-voucher read of <see cref="GetJournalEntryQuery"/> (null = 404 at the API).</summary>
public sealed class GetJournalEntryQueryHandler
    : IQueryHandler<GetJournalEntryQuery, JournalEntryDto?>
{
    private readonly IJournalRepository _journals;

    public GetJournalEntryQueryHandler(IJournalRepository journals)
    {
        _journals = journals;
    }

    public async Task<JournalEntryDto?> HandleAsync(
        GetJournalEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        var entry = await _journals.GetByIdAsync(query.JournalEntryId, cancellationToken);
        if (entry is null || entry.CompanyId != query.CompanyId)
        {
            return null;
        }

        return JournalEntryDto.Build(entry);
    }
}
