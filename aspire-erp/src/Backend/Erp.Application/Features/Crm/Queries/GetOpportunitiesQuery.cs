using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>
/// Loads the company's opportunities - the Block B pipeline board/list read (the Kanban
/// groups client-side, optionally pre-filtered by stage). Defaults to 50.
/// </summary>
public sealed record GetOpportunitiesQuery(Guid CompanyId, int Limit = 50, string? Stage = null)
    : IQuery<IReadOnlyList<OpportunityDto>>;
