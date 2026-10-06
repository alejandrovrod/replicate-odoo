using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>Loads the most recent leads of a company - the Block B list read. Paginated (Standard Pagination Pattern): page 1 of 50 by default.</summary>
public sealed record GetLeadsQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<LeadDto>>;
