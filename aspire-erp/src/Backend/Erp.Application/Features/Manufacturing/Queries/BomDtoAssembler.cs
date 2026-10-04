using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>
/// Shared assembler for the Task 9.5 BOM reads: resolves item codes/names and workstation
/// names plus composite hourly rates around the repository-loaded aggregates, and derives
/// each operation's cost through <see cref="BomOperation.CalculateCost"/> (the same pure
/// Domain math the posting engine absorbs, displayed - not posted - here).
/// </summary>
internal static class BomDtoAssembler
{
    public static async Task<IReadOnlyList<BomDto>> BuildAsync(
        IReadOnlyList<BillOfMaterials> boms,
        IManufacturingRepository manufacturing,
        IItemRepository items,
        CancellationToken cancellationToken)
    {
        var itemIds = new HashSet<Guid>();
        foreach (var bom in boms)
        {
            itemIds.Add(bom.ItemId);
            foreach (var line in bom.Items)
            {
                itemIds.Add(line.ItemId);
            }
        }

        var found = await items.GetByIdsAsync(new List<Guid>(itemIds), cancellationToken);
        var itemById = new Dictionary<Guid, Item>(found.Count);
        foreach (var item in found)
        {
            itemById[item.Id] = item;
        }

        var result = new List<BomDto>(boms.Count);
        foreach (var bom in boms)
        {
            itemById.TryGetValue(bom.ItemId, out var finished);

            var lines = new List<BomItemDto>(bom.Items.Count);
            foreach (var line in bom.Items.OrderBy(i => i.ItemId))
            {
                itemById.TryGetValue(line.ItemId, out var component);
                lines.Add(new BomItemDto(
                    line.Id,
                    line.ItemId,
                    component?.ItemCode ?? line.ItemId.ToString(),
                    component?.ItemName ?? string.Empty,
                    line.Quantity,
                    line.ValuationRate,
                    line.Amount,
                    line.ScrapPercentage));
            }

            var operations = new List<BomOperationDto>(bom.Operations.Count);
            foreach (var operation in bom.Operations.OrderBy(o => o.Description))
            {
                var workstation = await manufacturing.GetWorkstationByIdAsync(operation.WorkstationId, cancellationToken);
                var rate = workstation?.HourRateTotal ?? 0m;
                operations.Add(new BomOperationDto(
                    operation.Id,
                    operation.WorkstationId,
                    workstation?.WorkstationName ?? operation.WorkstationId.ToString(),
                    rate,
                    operation.Description,
                    operation.DurationMinutes,
                    BomOperation.CalculateCost(operation.DurationMinutes, rate)));
            }

            result.Add(new BomDto(
                bom.Id,
                bom.CompanyId,
                bom.BomNumber,
                bom.ItemId,
                finished?.ItemCode ?? bom.ItemId.ToString(),
                finished?.ItemName ?? string.Empty,
                bom.Quantity,
                bom.IsActive,
                bom.IsDefault,
                bom.RawMaterialCost,
                bom.OperatingCost,
                bom.ScrapCost,
                bom.TotalCost,
                lines,
                operations));
        }

        return result;
    }
}
