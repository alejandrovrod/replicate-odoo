using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Loads ONE journal entry with its lines, or null when it does not exist in this tenant/company
/// - the API turns null into RFC 7807 404.
/// </summary>
public sealed record GetJournalEntryQuery(Guid CompanyId, Guid JournalEntryId)
    : IQuery<JournalEntryDto?>;
