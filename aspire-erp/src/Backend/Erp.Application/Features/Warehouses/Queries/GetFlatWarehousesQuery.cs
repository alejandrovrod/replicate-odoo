using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Warehouses.Queries;

/// <summary>
/// Flat paged warehouses for card grids (Standard Pagination Pattern) - the companion to the
/// hierarchical tree read: same rows, no nesting, with <c>leavesOnly</c> and <c>isActive</c>
/// filters for directory-style views.
/// </summary>
public sealed record GetFlatWarehousesQuery(
    Guid CompanyId,
    bool LeavesOnly = false,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 50) : IQuery<PagedResult<WarehouseDto>>;
