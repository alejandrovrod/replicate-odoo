using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent purchase orders of one company with lines and supplier identity - the
/// list view behind the procurement workflow (Task 4.1). Paginated (Standard Pagination Pattern):
/// page 1 of 50 by default.
/// </summary>
public sealed record GetPurchaseOrdersQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<PurchaseOrderDto>>;
