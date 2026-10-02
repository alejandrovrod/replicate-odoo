using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent purchase orders of one company with lines and supplier identity - the
/// list view behind the procurement workflow (Task 4.1). Defaults to the 50 newest orders.
/// </summary>
public sealed record GetPurchaseOrdersQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<PurchaseOrderDto>>;
