using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Assets.Events;
using AssetHub.Application.Incidents.Events;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using AssetHub.Domain.Incidents;

namespace AssetHub.Application.Incidents.EventHandlers;

public class IncidentClosedEventHandler : INotificationHandler<IncidentStateChangedEvent>
{
    private readonly ITenantDbContext _db;
    private readonly ILogger<IncidentClosedEventHandler> _logger;
    private readonly IMediator _mediator;

    public IncidentClosedEventHandler(ITenantDbContext db, ILogger<IncidentClosedEventHandler> logger, IMediator mediator)
    {
        _db = db;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task Handle(IncidentStateChangedEvent notification, CancellationToken cancellationToken)
    {
        // Only trigger auto-revert if the incident reached a terminal state
        if (!notification.IsTerminal)
            return;

        if (notification.AssetId == Guid.Empty)
            return;

        var asset = await _db.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == notification.AssetId, cancellationToken);

        if (asset == null || asset.AssetTemplate == null)
            return;

        var config = asset.AssetTemplate.LifecycleStates;
        if (config == null) return;

        string initialState = config.InitialState;
        
        if (string.IsNullOrEmpty(initialState))
            return;

        // Quitamos la restricción de que aborte si ya está en estado inicial.
        // Siempre forzamos el disparo del evento para reevaluar la jerarquía del abuelo.

        var otherIncidents = await _db.Incidents
            .Include(i => i.WorkflowTemplate)
            .Where(i => i.AssetId == asset.Id && i.Id != notification.IncidentId && !i.IsDeleted)
            .ToListAsync(cancellationToken);

        bool hasOtherActiveIncidents = false;
        foreach (var i in otherIncidents)
        {
            bool isOtherTerminal = false;
            if (i.WorkflowTemplate != null && i.WorkflowTemplate.LifecycleStates != null && 
                i.WorkflowTemplate.LifecycleStates.States != null && 
                i.WorkflowTemplate.LifecycleStates.States.TryGetValue(i.State, out var stateConfig))
            {
                isOtherTerminal = stateConfig.IsTerminal;
            }
            else
            {
                isOtherTerminal = IncidentStates.TerminalStates.Contains(i.State);
            }

            if (!isOtherTerminal)
            {
                hasOtherActiveIncidents = true;
                break;
            }
        }

        if (hasOtherActiveIncidents)
        {
            _logger.LogInformation("Activo {AssetId} tiene otras incidencias activas. Omitiendo liberación por cierre de {IncidentId}", asset.Id, notification.IncidentId);
            return;
        }

        _logger.LogInformation("Liberando/Re-evaluando activo {AssetId} hacia estado {State} tras cerrar incidencia {IncidentId}", 
            asset.Id, initialState, notification.IncidentId);

        var fromState = asset.State;
        
        // Solo actualizamos la base de datos si el estado realmente cambia
        if (asset.State != initialState)
        {
            asset.State = initialState;

            _db.AssetLifecycleEvents.Add(new Domain.Assets.AssetLifecycleEvent
            {
                AssetId = asset.Id,
                EventType = "auto_revert",
                FromState = fromState,
                ToState = initialState,
                Notes = $"Liberación automática tras cerrar incidencia {notification.IncidentId}",
                At = DateTime.UtcNow,
                UserId = Guid.Empty // Sistema
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        
        // Disparar evento para propagación al padre y OnEnterAction
        await _mediator.Publish(new AssetStateChangedEvent(
            asset.Id,
            fromState,
            initialState,
            asset.AssetTemplate.Name
        ), cancellationToken);
    }
}
