using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Incidents.Commands;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Domain.Incidents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Incidents.EventHandlers;

public class MaintenanceOrderCompletedEventHandler : INotificationHandler<MaintenanceOrderCompletedEvent>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<MaintenanceOrderCompletedEventHandler> _logger;

    public MaintenanceOrderCompletedEventHandler(
        ITenantDbContext db,
        IMediator mediator, 
        ILogger<MaintenanceOrderCompletedEventHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(MaintenanceOrderCompletedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.IncidentId == null)
            return;

        try
        {
            var incident = await _db.Incidents
                .Include(i => i.WorkflowTemplate)
                .FirstOrDefaultAsync(i => i.Id == notification.IncidentId.Value, cancellationToken);

            if (incident == null)
                return;

            string targetState = IncidentStates.Resolved;

            // Opción A: Buscar estado terminal dinámicamente en la plantilla
            if (incident.WorkflowTemplate?.LifecycleStates?.States != null)
            {
                var terminalStates = incident.WorkflowTemplate.LifecycleStates.States
                    .Where(kvp => kvp.Value.IsTerminal)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (terminalStates.Any())
                {
                    // Evitar elegir el estado de "cancelado" si hay otro terminal válido
                    var successTerminal = terminalStates.FirstOrDefault(s => 
                        !s.Contains("cancel", StringComparison.OrdinalIgnoreCase) && 
                        !s.Contains("rechaz", StringComparison.OrdinalIgnoreCase)
                    );
                    
                    targetState = successTerminal ?? terminalStates.First();
                }
                else
                {
                    // Fallback heurístico si no hay estados marcados isTerminal
                    var keys = incident.WorkflowTemplate.LifecycleStates.States.Keys;
                    targetState = keys.FirstOrDefault(k => 
                        k.Contains("resolv", StringComparison.OrdinalIgnoreCase) || 
                        k.Contains("termina", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("cerra", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("close", StringComparison.OrdinalIgnoreCase)
                    ) ?? targetState;
                }
            }

            await _mediator.Send(new ChangeIncidentStateCommand
            {
                IncidentId = notification.IncidentId.Value,
                TargetState = targetState,
                PropertiesJson = null,
                ForceTransition = true
            }, cancellationToken);
            
            _logger.LogInformation("Automatically resolved incident {IncidentId} to state {TargetState} because maintenance order {OrderId} was completed", notification.IncidentId, targetState, notification.MaintenanceOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to automatically resolve incident {IncidentId} when maintenance order {OrderId} completed. The incident might have custom lifecycle rules that prevent this transition.", notification.IncidentId, notification.MaintenanceOrderId);
        }
    }
}
