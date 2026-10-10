using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Incidents.Queries;

public record GetIncidentTimelineQuery(Guid IncidentId) : IRequest<List<IncidentTimelineDto>>;

public class IncidentTimelineDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? PropertiesJson { get; set; }
    public DateTime At { get; set; }
    public Guid UserId { get; set; }
}

public class GetIncidentTimelineQueryHandler : IRequestHandler<GetIncidentTimelineQuery, List<IncidentTimelineDto>>
{
    private readonly ITenantDbContext _db;

    public GetIncidentTimelineQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<IncidentTimelineDto>> Handle(GetIncidentTimelineQuery request, CancellationToken cancellationToken)
    {
        var events = await _db.IncidentLifecycleEvents
            .Where(e => e.IncidentId == request.IncidentId)
            .OrderByDescending(e => e.At)
            .Select(e => new IncidentTimelineDto
            {
                Id = e.Id,
                EventType = e.EventType,
                FromState = e.FromState,
                ToState = e.ToState,
                Notes = e.Notes,
                PropertiesJson = e.PropertiesJson,
                At = e.At,
                UserId = e.UserId
            })
            .ToListAsync(cancellationToken);

        return events;
    }
}
