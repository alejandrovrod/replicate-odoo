using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Lists one company's asset headers for the Block C asset workbench. Read-only.</summary>
public sealed class GetAssetsQueryHandler : IQueryHandler<GetAssetsQuery, PagedResult<AssetDto>>
{
    private readonly IAssetsRepository _assets;

    public GetAssetsQueryHandler(IAssetsRepository assets)
    {
        _assets = assets;
    }

    public async Task<PagedResult<AssetDto>> HandleAsync(
        GetAssetsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _assets.GetAssetsByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        var result = new List<AssetDto>(page.Items.Count);
        foreach (var asset in page.Items)
        {
            result.Add(AssetDto.Build(asset));
        }

        return page.Map(result);
    }
}
