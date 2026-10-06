using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Loads the most recent journal entries of one company with their lines - the list view behind
/// the manual-voucher screen (and the read side tasks 2.5/2.6 will reuse). Paginated (Standard
/// Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetJournalEntriesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<JournalEntryDto>>;
