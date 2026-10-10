using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Analytics.Queries;

public class TimelineEventDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty; // Order, Incident, Lifecycle, Custom
    public string Description { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
}

public class GetAssetTimelineQuery : IRequest<List<TimelineEventDto>>
{
    public Guid AssetId { get; set; }
}

public class GetAssetTimelineQueryHandler : IRequestHandler<GetAssetTimelineQuery, List<TimelineEventDto>>
{
    private readonly ITenantDbContext _db;

    public GetAssetTimelineQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<TimelineEventDto>> Handle(GetAssetTimelineQuery request, CancellationToken cancellationToken)
    {
        var orders = await _db.MaintenanceOrders
            .Where(o => o.AssetId == request.AssetId)
            .Select(o => new TimelineEventDto
            {
                Date = o.ScheduledStart ?? o.CompletedAt ?? DateTime.UtcNow,
                Type = "Order",
                Description = $"Order {o.State}",
                ReferenceId = o.Id
            })
            .ToListAsync(cancellationToken);

        var incidents = await _db.Incidents
            .Where(i => i.AssetId == request.AssetId)
            .Select(i => new TimelineEventDto
            {
                Date = i.ReportedAt,
                Type = "Incident",
                Description = $"Incident: {i.Title}",
                ReferenceId = i.Id
            })
            .ToListAsync(cancellationToken);

        var lifecycleEvents = await _db.AssetLifecycleEvents
            .Where(e => e.AssetId == request.AssetId)
            .Select(e => new TimelineEventDto
            {
                Date = e.At,
                Type = "Lifecycle",
                Description = e.EventType + " " + e.Notes,
                ReferenceId = e.Id
            })
            .ToListAsync(cancellationToken);

        var allEvents = orders.Concat(incidents).Concat(lifecycleEvents)
            .OrderByDescending(e => e.Date)
            .ToList();

        return allEvents;
    }
}
