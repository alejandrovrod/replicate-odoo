using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Lists one company's asset categories for the Block C asset workbench. Read-only.</summary>
public sealed class GetAssetCategoriesQueryHandler
    : IQueryHandler<GetAssetCategoriesQuery, PagedResult<AssetCategoryDto>>
{
    private readonly IAssetsRepository _assets;

    public GetAssetCategoriesQueryHandler(IAssetsRepository assets)
    {
        _assets = assets;
    }

    public async Task<PagedResult<AssetCategoryDto>> HandleAsync(
        GetAssetCategoriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _assets.GetCategoriesByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        var result = new List<AssetCategoryDto>(page.Items.Count);
        foreach (var category in page.Items)
        {
            result.Add(AssetCategoryDto.Build(category));
        }

        return page.Map(result);
    }
}
