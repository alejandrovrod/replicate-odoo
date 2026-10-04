using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Lists one company's asset categories for the Block C asset workbench. Read-only.</summary>
public sealed class GetAssetCategoriesQueryHandler
    : IQueryHandler<GetAssetCategoriesQuery, IReadOnlyList<AssetCategoryDto>>
{
    private readonly IAssetsRepository _assets;

    public GetAssetCategoriesQueryHandler(IAssetsRepository assets)
    {
        _assets = assets;
    }

    public async Task<IReadOnlyList<AssetCategoryDto>> HandleAsync(
        GetAssetCategoriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var categories = await _assets.GetCategoriesByCompanyAsync(query.CompanyId, cancellationToken);
        var result = new List<AssetCategoryDto>(categories.Count);
        foreach (var category in categories)
        {
            result.Add(AssetCategoryDto.Build(category));
        }

        return result;
    }
}
