using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Lists one company's work-order headers (Task 9.5 execution board reads). Paginated (Standard Pagination Pattern): page 1 of 50 by default.</summary>
public sealed record GetWorkOrdersQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<WorkOrderDto>>;
