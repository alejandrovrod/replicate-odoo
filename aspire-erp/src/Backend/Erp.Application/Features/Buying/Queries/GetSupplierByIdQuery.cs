using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads ONE supplier by id (with its per-company accounts and group), or null when it does not
/// exist in this tenant - the API turns null into RFC 7807 404. Mirrors
/// <see cref="Selling.Queries.GetCustomerByIdQuery"/>.
/// </summary>
public sealed record GetSupplierByIdQuery(Guid SupplierId) : IQuery<SupplierDto?>;
