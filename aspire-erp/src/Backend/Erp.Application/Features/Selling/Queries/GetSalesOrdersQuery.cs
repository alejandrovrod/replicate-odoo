using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads the most recent sales orders of one company with lines and customer identity - the list
/// view behind the selling workflow (Task 5.2). Paginated (Standard Pagination Pattern): page 1
/// of 50 by default.
/// </summary>
public sealed record GetSalesOrdersQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<SalesOrderDto>>;
