using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Commands;

public record PredictionItemDto(
    Guid AssetId,
    decimal RiskProbability,
    string RiskLevel,
    int? PredictedFailureDays,
    string? TopFeatureContributionsJson
);

public record RecordAssetPredictionBatchCommand(List<PredictionItemDto> Predictions) : IRequest<int>;

public class RecordAssetPredictionBatchCommandHandler : IRequestHandler<RecordAssetPredictionBatchCommand, int>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public RecordAssetPredictionBatchCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<int> Handle(RecordAssetPredictionBatchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId() ?? Guid.Empty;
        if (request.Predictions == null || request.Predictions.Count == 0)
        {
            return 0;
        }

        var assetIds = request.Predictions.Select(p => p.AssetId).Distinct().ToList();
        var validAssets = await _dbContext.Assets
            .Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        int count = 0;
        var now = DateTime.UtcNow;

        foreach (var item in request.Predictions)
        {
            if (!validAssets.TryGetValue(item.AssetId, out var asset))
            {
                continue;
            }

            var prediction = new AssetHealthPrediction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AssetId = item.AssetId,
                RiskProbability = Math.Clamp(item.RiskProbability, 0m, 1m),
                RiskLevel = item.RiskLevel ?? "Low",
                PredictedFailureDays = item.PredictedFailureDays,
                TopFeatureContributionsJson = item.TopFeatureContributionsJson,
                CreatedAt = now
            };

            await _dbContext.AssetHealthPredictions.AddAsync(prediction, cancellationToken);
            count++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }
}
