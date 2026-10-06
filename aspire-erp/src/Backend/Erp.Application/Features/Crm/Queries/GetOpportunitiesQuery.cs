using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>
/// Loads the company's opportunities - the Block B pipeline board/list read (the Kanban
/// groups client-side, optionally pre-filtered by stage). Paginated (Standard Pagination
/// Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetOpportunitiesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50, string? Stage = null)
    : IQuery<PagedResult<OpportunityDto>>;
