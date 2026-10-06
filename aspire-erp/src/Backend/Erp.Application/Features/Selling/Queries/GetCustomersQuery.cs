using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads the most recent customers of a company - the picker behind quotations and sales orders
/// (Task 5.1). Customers are company-scoped (plan.md §1), so the company is required. Paginated
/// (Standard Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetCustomersQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<CustomerDto>>;
