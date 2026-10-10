using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Incidents.Events;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Domain.Incidents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AssetHub.Application.Interfaces;

namespace AssetHub.Application.Maintenance.EventHandlers;

public class MaintenanceOrderVerifiedEventHandler : INotificationHandler<MaintenanceOrderVerifiedEvent>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;

    public MaintenanceOrderVerifiedEventHandler(ITenantDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task Handle(MaintenanceOrderVerifiedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.IncidentId != null)
        {
            var incident = await _db.Incidents
                .FirstOrDefaultAsync(i => i.Id == notification.IncidentId.Value && i.TenantId == notification.TenantId, cancellationToken);

            if (incident != null && !IncidentStates.TerminalStates.Contains(incident.State))
            {
                var fromState = incident.State;
                incident.State = IncidentStates.Closed;
                incident.ClosedAt = DateTime.UtcNow;

                _db.IncidentLifecycleEvents.Add(new IncidentLifecycleEvent
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    EventType = "cambio de estado",
                    FromState = fromState,
                    ToState = IncidentStates.Closed,
                    Notes = $"Cierre automático tras verificar orden de mantenimiento {notification.MaintenanceOrderId}",
                    PropertiesJson = "{}",
                    At = DateTime.UtcNow,
                    UserId = Guid.Empty // Sistema
                });

                await _db.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new IncidentClosedEvent(
                    incident.Id,
                    incident.AssetId,
                    incident.TenantId,
                    IncidentStates.Closed
                ), cancellationToken);
            }
        }
        else
        {
            // Direct order verified (no incident)
            var asset = await _db.Assets
                .Include(a => a.AssetTemplate)
                .FirstOrDefaultAsync(a => a.Id == notification.AssetId, cancellationToken);

            if (asset != null && asset.AssetTemplate != null && asset.AssetTemplate.LifecycleStates != null)
            {
                var config = asset.AssetTemplate.LifecycleStates;
                if (config.States.TryGetValue(asset.State, out var currentStateConfig))
                {
                    // Check if asset is blocked by WORK_ORDERS module
                    if (currentStateConfig.AssociatedModule == "WORK_ORDERS" || currentStateConfig.AssociatedModule == "work_orders")
                    {
                        var hasActiveOrders = await _db.MaintenanceOrders
                            .AnyAsync(o => o.AssetId == asset.Id 
                                        && o.Id != notification.MaintenanceOrderId 
                                        && !o.IsDeleted 
                                        && o.State != Domain.Maintenance.MaintenanceOrderStates.Verified 
                                        && o.State != Domain.Maintenance.MaintenanceOrderStates.Cancelled, 
                                        cancellationToken);

                        if (!hasActiveOrders && !string.IsNullOrEmpty(config.InitialState))
                        {
                            var fromAssetState = asset.State;
                            if (asset.State != config.InitialState)
                            {
                                asset.State = config.InitialState;
                                
                                _db.AssetLifecycleEvents.Add(new Domain.Assets.AssetLifecycleEvent
                                {
                                    AssetId = asset.Id,
                                    EventType = "auto_revert",
                                    FromState = fromAssetState,
                                    ToState = config.InitialState,
                                    Notes = $"Liberación automática tras verificar orden de mantenimiento {notification.MaintenanceOrderId}",
                                    At = DateTime.UtcNow,
                                    UserId = Guid.Empty // Sistema
                                });

                                await _db.SaveChangesAsync(cancellationToken);
                            }

                            await _mediator.Publish(new AssetHub.Application.Assets.Events.AssetStateChangedEvent(
                                asset.Id,
                                fromAssetState,
                                config.InitialState,
                                asset.AssetTemplate.Name
                            ), cancellationToken);
                        }
                    }
                }
            }
        }
    }
}
