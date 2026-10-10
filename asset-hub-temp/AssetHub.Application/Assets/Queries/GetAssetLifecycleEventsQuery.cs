using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Queries;

public record AssetLifecycleEventDto
{
    public Guid Id { get; init; }
    public Guid AssetId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string FromState { get; init; } = string.Empty;
    public string ToState { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime At { get; init; }
    public Guid UserId { get; init; }
}

public record GetAssetLifecycleEventsQuery(Guid AssetId) : IRequest<List<AssetLifecycleEventDto>>;

public class GetAssetLifecycleEventsQueryHandler : IRequestHandler<GetAssetLifecycleEventsQuery, List<AssetLifecycleEventDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetAssetLifecycleEventsQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AssetLifecycleEventDto>> Handle(GetAssetLifecycleEventsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.AssetLifecycleEvents
            .Where(e => e.AssetId == request.AssetId)
            .OrderByDescending(e => e.At)
            .Select(e => new AssetLifecycleEventDto
            {
                Id = e.Id,
                AssetId = e.AssetId,
                EventType = e.EventType,
                FromState = e.FromState,
                ToState = e.ToState,
                Notes = e.Notes,
                At = e.At,
                UserId = e.UserId
            })
            .ToListAsync(cancellationToken);
    }
}
