using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Warehouses.Queries;

/// <summary>
/// Loads the hierarchical warehouse tree of one company (roots at the top) - the data source of
/// Task 3.4's warehouse view. Unknown/empty companies simply yield an empty tree.
/// </summary>
public sealed record GetWarehousesQuery(Guid CompanyId) : IQuery<IReadOnlyList<WarehouseTreeNodeDto>>;
