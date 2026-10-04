using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads the most recent customers of a company - the picker behind quotations and sales orders
/// (Task 5.1). Customers are company-scoped (plan.md §1), so the company is required. Defaults
/// to 50.
/// </summary>
public sealed record GetCustomersQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<CustomerDto>>;
