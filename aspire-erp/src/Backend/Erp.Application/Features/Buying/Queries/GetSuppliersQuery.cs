using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent suppliers of the tenant - the picker behind new purchase orders and the
/// supplier list view. Suppliers are tenant-wide (like items), so no company filter is mandatory,
/// but <see cref="GroupId"/>, <see cref="OnHold"/> and <see cref="CompanyId"/> narrow the list
/// (module buying-suppliers spec §5). Paginated (Standard Pagination Pattern): page 1 of 50.
/// </summary>
public sealed record GetSuppliersQuery(
    int PageNumber = 1,
    int PageSize = 50,
    Guid? GroupId = null,
    bool? OnHold = null,
    Guid? CompanyId = null) : IQuery<PagedResult<SupplierDto>>;
