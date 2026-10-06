using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Warehouses.Queries;

/// <summary>
/// Builds the nested warehouse tree: loads the company's warehouses flat through the repository
/// (tenant filtering automatic - Constitution II.3) and assembles the hierarchy in memory,
/// ordered by Code - the same assembly strategy as GetAccountTreeQueryHandler.
/// </summary>
public sealed class GetWarehousesQueryHandler : IQueryHandler<GetWarehousesQuery, IReadOnlyList<WarehouseTreeNodeDto>>
{
    private readonly IWarehouseRepository _warehouses;

    public GetWarehousesQueryHandler(IWarehouseRepository warehouses)
    {
        _warehouses = warehouses;
    }

    public async Task<IReadOnlyList<WarehouseTreeNodeDto>> HandleAsync(
        GetWarehousesQuery query,
        CancellationToken cancellationToken = default)
    {
        var all = await _warehouses.GetByCompanyAsync(query.CompanyId, cancellationToken);

        var ids = new HashSet<Guid>(all.Count);
        foreach (var warehouse in all)
        {
            ids.Add(warehouse.Id);
        }

        var childrenByParent = new Dictionary<Guid, List<Warehouse>>();
        foreach (var warehouse in all)
        {
            if (warehouse.ParentWarehouseId is not { } parentId || !ids.Contains(parentId))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                children = new List<Warehouse>();
                childrenByParent[parentId] = children;
            }

            children.Add(warehouse);
        }

        WarehouseTreeNodeDto BuildNode(Warehouse warehouse)
        {
            List<WarehouseTreeNodeDto>? childNodes = null;

            if (childrenByParent.TryGetValue(warehouse.Id, out var children))
            {
                children.Sort((x, y) => string.CompareOrdinal(x.WarehouseCode, y.WarehouseCode));
                childNodes = new List<WarehouseTreeNodeDto>(children.Count);
                foreach (var child in children)
                {
                    childNodes.Add(BuildNode(child));
                }
            }

            return new WarehouseTreeNodeDto(
                warehouse.Id,
                warehouse.WarehouseCode,
                warehouse.WarehouseName,
                warehouse.ParentWarehouseId,
                warehouse.AccountId ?? Guid.Empty,
                warehouse.IsGroup,
                warehouse.IsActive,
                warehouse.RowVersion,
                childNodes ?? (IReadOnlyList<WarehouseTreeNodeDto>)Array.Empty<WarehouseTreeNodeDto>());
        }

        var roots = new List<Warehouse>();
        foreach (var warehouse in all)
        {
            if (warehouse.ParentWarehouseId is null || !ids.Contains(warehouse.ParentWarehouseId.Value))
            {
                roots.Add(warehouse);
            }
        }

        roots.Sort((x, y) => string.CompareOrdinal(x.WarehouseCode, y.WarehouseCode));

        var tree = new List<WarehouseTreeNodeDto>(roots.Count);
        foreach (var root in roots)
        {
            tree.Add(BuildNode(root));
        }

        return tree;
    }
}

