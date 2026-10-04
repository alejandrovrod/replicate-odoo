using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads ONE sales order with its lines, or null when it does not exist in this tenant/company
/// - the API turns null into RFC 7807 404.
/// </summary>
public sealed record GetSalesOrderByIdQuery(Guid CompanyId, Guid SalesOrderId)
    : IQuery<SalesOrderDto?>;
