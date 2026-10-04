using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads the most recent sales orders of one company with lines and customer identity - the list
/// view behind the selling workflow (Task 5.2). Defaults to the 50 newest orders.
/// </summary>
public sealed record GetSalesOrdersQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<SalesOrderDto>>;
