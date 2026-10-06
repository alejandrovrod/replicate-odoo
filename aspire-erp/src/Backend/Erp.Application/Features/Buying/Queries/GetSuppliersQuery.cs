using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent suppliers of the tenant - the picker behind new purchase orders.
/// Suppliers are tenant-wide (like items), so no company filter applies. Paginated
/// (Standard Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetSuppliersQuery(int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<SupplierDto>>;
