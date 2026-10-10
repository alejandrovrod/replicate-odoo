using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Incidents.Commands;

public class TriageIncidentCommand : IRequest<Unit>
{
    public Guid IncidentId { get; set; }
    public Guid PriorityId { get; set; }
}

public class TriageIncidentCommandHandler : IRequestHandler<TriageIncidentCommand, Unit>
{
    private readonly ITenantDbContext _db;

    public TriageIncidentCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(TriageIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == request.IncidentId, cancellationToken);
        if (incident == null)
            throw new ArgumentException("Incident not found");
            
        if (incident.State != "reported")
            throw new InvalidOperationException($"Cannot triage incident in state {incident.State}");
            
        var priorityExists = await _db.CatalogItems.AnyAsync(c => c.Id == request.PriorityId, cancellationToken);
        if (!priorityExists)
            throw new ArgumentException("Priority catalog item not found");

        incident.PriorityId = request.PriorityId;
        incident.State = "triaged";
        
        await _db.SaveChangesAsync(cancellationToken);
        
        return Unit.Value;
    }
}
