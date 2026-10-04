using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>
/// Returns one company's asset with its schedule lines ordered by due date (Block B reads).
/// A foreign-company id resolves to null (never leaks across companies). Read-only.
/// </summary>
public sealed class GetAssetDetailQueryHandler : IQueryHandler<GetAssetDetailQuery, AssetDetailDto?>
{
    private readonly IAssetsRepository _assets;

    public GetAssetDetailQueryHandler(IAssetsRepository assets)
    {
        _assets = assets;
    }

    public async Task<AssetDetailDto?> HandleAsync(
        GetAssetDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        var asset = await _assets.GetAssetByIdAsync(query.AssetId, cancellationToken);
        if (asset is null || asset.CompanyId != query.CompanyId)
        {
            return null;
        }

        var schedules = await _assets.GetSchedulesByAssetAsync(asset.Id, cancellationToken);
        var lines = schedules
            .Select(l => new AssetScheduleLineDto(
                l.Id, l.ScheduleDate, l.DepreciationAmount, l.AccumulatedDepreciationAfter, l.Status))
            .ToList();

        return new AssetDetailDto(AssetDto.Build(asset), lines);
    }
}
