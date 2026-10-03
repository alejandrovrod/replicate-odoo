using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Loads the most recent journal entries of one company with their lines - the list view behind
/// the manual-voucher screen (and the read side tasks 2.5/2.6 will reuse). Defaults to the
/// 50 newest vouchers.
/// </summary>
public sealed record GetJournalEntriesQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<JournalEntryDto>>;
