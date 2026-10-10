using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Analytics.Queries;

public class AssetConditionPointDto
{
    public DateTime Date { get; set; }
    public decimal ConditionIndex { get; set; }
    public string? Reason { get; set; }
}

public class GetAssetConditionEvolutionQuery : IRequest<List<AssetConditionPointDto>>
{
    public Guid AssetId { get; set; }
}

public class GetAssetConditionEvolutionQueryHandler : IRequestHandler<GetAssetConditionEvolutionQuery, List<AssetConditionPointDto>>
{
    private readonly ITenantDbContext _db;

    public GetAssetConditionEvolutionQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<AssetConditionPointDto>> Handle(GetAssetConditionEvolutionQuery request, CancellationToken cancellationToken)
    {
        var history = await _db.AssetConditionHistories
            .Where(h => h.AssetId == request.AssetId)
            .OrderBy(h => h.CapturedAt)
            .Select(h => new AssetConditionPointDto
            {
                Date = h.CapturedAt,
                ConditionIndex = h.ConditionIndex,
                Reason = h.Reason
            })
            .ToListAsync(cancellationToken);

        return history;
    }
}
