using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Analytics.Queries;

public class AssetLifeProjectionDto
{
    public bool HasSufficientData { get; set; }
    public DateTime? EstimatedEndOfLife { get; set; }
    public decimal? DegradationRatePerDay { get; set; }
    public string? Message { get; set; }
}

public class GetAssetLifeProjectionQuery : IRequest<AssetLifeProjectionDto>
{
    public Guid AssetId { get; set; }
    public decimal EndOfLifeThreshold { get; set; } = 20m;
}

public class GetAssetLifeProjectionQueryHandler : IRequestHandler<GetAssetLifeProjectionQuery, AssetLifeProjectionDto>
{
    private readonly ITenantDbContext _db;

    public GetAssetLifeProjectionQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<AssetLifeProjectionDto> Handle(GetAssetLifeProjectionQuery request, CancellationToken cancellationToken)
    {
        var points = await _db.AssetConditionHistories
            .Where(h => h.AssetId == request.AssetId)
            .OrderBy(h => h.CapturedAt)
            .Select(h => new { h.CapturedAt, h.ConditionIndex })
            .ToListAsync(cancellationToken);

        if (points.Count < 3)
        {
            return new AssetLifeProjectionDto
            {
                HasSufficientData = false,
                Message = "Insufficient data. At least 3 condition records are required."
            };
        }

        // Linear Regression: y = mx + b
        // x = days since first point, y = condition
        var firstDate = points.First().CapturedAt;
        
        var xValues = points.Select(p => (p.CapturedAt - firstDate).TotalDays).ToArray();
        var yValues = points.Select(p => (double)p.ConditionIndex).ToArray();

        var n = points.Count;
        var sumX = xValues.Sum();
        var sumY = yValues.Sum();
        var sumX2 = xValues.Sum(x => x * x);
        var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();

        var denominator = (n * sumX2) - (sumX * sumX);
        if (Math.Abs(denominator) < 1e-10)
        {
            return new AssetLifeProjectionDto
            {
                HasSufficientData = false,
                Message = "Data points do not have enough variance in time."
            };
        }

        var m = ((n * sumXY) - (sumX * sumY)) / denominator;
        var b = (sumY - (m * sumX)) / n;

        if (m >= 0)
        {
            return new AssetLifeProjectionDto
            {
                HasSufficientData = true,
                DegradationRatePerDay = (decimal)m,
                Message = "Condition is not degrading over time based on current data."
            };
        }

        // Find x where y = threshold
        // mx + b = threshold => x = (threshold - b) / m
        var xAtThreshold = ((double)request.EndOfLifeThreshold - b) / m;
        
        // If xAtThreshold is in the past, or very far in the future
        var estimatedDate = firstDate.AddDays(xAtThreshold);

        return new AssetLifeProjectionDto
        {
            HasSufficientData = true,
            DegradationRatePerDay = (decimal)m,
            EstimatedEndOfLife = estimatedDate
        };
    }
}
