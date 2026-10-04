using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Lists one company's asset headers for the Block C asset workbench. Read-only.</summary>
public sealed class GetAssetsQueryHandler : IQueryHandler<GetAssetsQuery, IReadOnlyList<AssetDto>>
{
    private readonly IAssetsRepository _assets;

    public GetAssetsQueryHandler(IAssetsRepository assets)
    {
        _assets = assets;
    }

    public async Task<IReadOnlyList<AssetDto>> HandleAsync(
        GetAssetsQuery query,
        CancellationToken cancellationToken = default)
    {
        var assets = await _assets.GetAssetsByCompanyAsync(query.CompanyId, cancellationToken);
        var result = new List<AssetDto>(assets.Count);
        foreach (var asset in assets)
        {
            result.Add(AssetDto.Build(asset));
        }

        return result;
    }
}
