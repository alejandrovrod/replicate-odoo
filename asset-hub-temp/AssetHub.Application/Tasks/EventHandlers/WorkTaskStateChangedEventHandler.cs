using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Application.Tasks.Events;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AssetHub.Application.Interfaces;

namespace AssetHub.Application.Tasks.EventHandlers;

public class WorkTaskStateChangedEventHandler : INotificationHandler<WorkTaskStateChangedEvent>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;

    public WorkTaskStateChangedEventHandler(ITenantDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task Handle(WorkTaskStateChangedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.MaintenanceOrderId == null)
            return;

        if (notification.ToState != WorkTaskStates.Done && notification.ToState != WorkTaskStates.Cancelled)
            return;

        var orderId = notification.MaintenanceOrderId.Value;

        var order = await _db.MaintenanceOrders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == notification.TenantId, cancellationToken);

        if (order == null || order.State != MaintenanceOrderStates.InProgress)
            return;

        var activeTasks = await _db.WorkTasks
            .Where(t => t.MaintenanceOrderId == orderId && t.TenantId == notification.TenantId && !t.IsDeleted)
            .ToListAsync(cancellationToken);
        var allTerminal = activeTasks.All(t => WorkTaskStates.TerminalStates.Contains(t.State));
        var anyDone = activeTasks.Any(t => t.State == WorkTaskStates.Done);

        if (allTerminal && anyDone)
        {
            order.State = MaintenanceOrderStates.Done;
            order.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await _mediator.Publish(new MaintenanceOrderCompletedEvent(
                order.Id,
                order.TenantId,
                order.AssetId,
                order.IncidentId
            ), cancellationToken);
        }
    }
}
