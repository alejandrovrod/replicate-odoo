using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Queries;

public record AssetHealthForecastDto(
    Guid AssetId,
    decimal RiskProbability,
    string RiskLevel,
    int? PredictedFailureDays,
    string? TopFeatureContributionsJson,
    DateTime CreatedAt
);

public record GetAssetHealthForecastQuery(Guid AssetId) : IRequest<AssetHealthForecastDto?>;

public class GetAssetHealthForecastQueryHandler : IRequestHandler<GetAssetHealthForecastQuery, AssetHealthForecastDto?>
{
    private readonly ITenantDbContext _dbContext;

    public GetAssetHealthForecastQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AssetHealthForecastDto?> Handle(GetAssetHealthForecastQuery request, CancellationToken cancellationToken)
    {
        var latest = await _dbContext.AssetHealthPredictions
            .Where(p => p.AssetId == request.AssetId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AssetHealthForecastDto(
                p.AssetId,
                p.RiskProbability,
                p.RiskLevel,
                p.PredictedFailureDays,
                p.TopFeatureContributionsJson,
                p.CreatedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

        return latest;
    }
}
