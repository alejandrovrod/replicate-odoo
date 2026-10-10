using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Queries;

public record GetAssetMaterialsQuery(Guid AssetId, bool? IsCritical, string? Search) : IRequest<List<AssetMaterialDto>>;

public record AssetMaterialDto(
    Guid Id,
    Guid AssetId,
    Guid CatalogItemId,
    string CatalogItemCode,
    string CatalogItemLabel,
    decimal Quantity,
    string UnitOfMeasure,
    bool IsCritical,
    string? Notes
);

public class GetAssetMaterialsQueryHandler : IRequestHandler<GetAssetMaterialsQuery, List<AssetMaterialDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetAssetMaterialsQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AssetMaterialDto>> Handle(GetAssetMaterialsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AssetMaterials
            .Include(m => m.CatalogItem)
            .Where(m => m.AssetId == request.AssetId);

        if (request.IsCritical.HasValue)
        {
            query = query.Where(m => m.IsCritical == request.IsCritical.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(m => m.CatalogItem!.Code.ToLower().Contains(search) || 
                                     m.CatalogItem.Translations.Any(t => t.Label.ToLower().Contains(search)));
        }

        var materials = await query
            .OrderBy(m => m.CatalogItem!.Code)
            .Select(m => new AssetMaterialDto(
                m.Id,
                m.AssetId,
                m.CatalogItemId,
                m.CatalogItem!.Code,
                m.CatalogItem.Translations.Select(t => t.Label).FirstOrDefault() ?? m.CatalogItem.Code,
                m.Quantity,
                m.UnitOfMeasure,
                m.IsCritical,
                m.Notes
            ))
            .ToListAsync(cancellationToken);

        return materials;
    }
}
