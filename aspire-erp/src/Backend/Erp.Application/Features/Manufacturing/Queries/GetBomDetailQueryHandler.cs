using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Loads one BOM detail for the Task 9.5 tree editor. Null when missing or foreign.</summary>
public sealed class GetBomDetailQueryHandler : IQueryHandler<GetBomDetailQuery, BomDto?>
{
    private readonly IManufacturingRepository _manufacturing;
    private readonly IItemRepository _items;

    public GetBomDetailQueryHandler(IManufacturingRepository manufacturing, IItemRepository items)
    {
        _manufacturing = manufacturing;
        _items = items;
    }

    public async Task<BomDto?> HandleAsync(
        GetBomDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        var bom = await _manufacturing.GetBomByIdAsync(query.BomId, cancellationToken);
        if (bom is null || bom.CompanyId != query.CompanyId)
        {
            return null;
        }

        var built = await BomDtoAssembler.BuildAsync(
            new[] { bom }, _manufacturing, _items, cancellationToken);
        return built[0];
    }
}
