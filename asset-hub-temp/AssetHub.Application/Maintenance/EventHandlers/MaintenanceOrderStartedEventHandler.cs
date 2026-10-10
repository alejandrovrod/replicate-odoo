using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AssetHub.Application.Interfaces;

namespace AssetHub.Application.Maintenance.EventHandlers;

public class MaintenanceOrderStartedEventHandler : INotificationHandler<MaintenanceOrderStartedEvent>
{
    private readonly ITenantDbContext _db;

    public MaintenanceOrderStartedEventHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task Handle(MaintenanceOrderStartedEvent notification, CancellationToken cancellationToken)
    {
        var pendingTasks = await _db.WorkTasks
            .Where(t =>
                t.MaintenanceOrderId == notification.MaintenanceOrderId &&
                t.TenantId == notification.TenantId &&
                !t.IsDeleted &&
                t.State == WorkTaskStates.Todo)
            .ToListAsync(cancellationToken);

        foreach (var task in pendingTasks)
        {
            task.State = WorkTaskStates.InProgress;
            task.StartedAt = DateTime.UtcNow;

            _db.TaskStatusHistories.Add(new TaskStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = task.TenantId,
                WorkTaskId = task.Id,
                FromState = WorkTaskStates.Todo,
                ToState = WorkTaskStates.InProgress,
                ChangedByUserId = Guid.Empty, // Sistema
                ChangedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
