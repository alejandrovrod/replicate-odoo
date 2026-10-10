using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Notifications;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class EvaluateDueWorkTasksCommand : IRequest<EvaluateDueWorkTasksResult>
{
    public int HoursThreshold { get; set; } = 24;
}

public class EvaluateDueWorkTasksResult
{
    public int EvaluatedTasks { get; set; }
    public int NotificationsSent { get; set; }
}

public class EvaluateDueWorkTasksCommandHandler : IRequestHandler<EvaluateDueWorkTasksCommand, EvaluateDueWorkTasksResult>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public EvaluateDueWorkTasksCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<EvaluateDueWorkTasksResult> Handle(EvaluateDueWorkTasksCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()!.Value;
        var threshold = DateTime.UtcNow.AddHours(request.HoursThreshold);

        var dueTasks = await _db.WorkTasks
            .AsNoTracking()
            .Include(t => t.AssignedEmployee)
            .Where(t =>
                t.TenantId == tenantId &&
                !WorkTaskStates.TerminalStates.Contains(t.State) &&
                t.DueAt.HasValue &&
                t.DueAt.Value <= threshold &&
                t.AssignedEmployeeId.HasValue &&
                t.AssignedEmployee != null &&
                t.AssignedEmployee.UserId.HasValue)
            .ToListAsync(cancellationToken);

        var result = new EvaluateDueWorkTasksResult
        {
            EvaluatedTasks = dueTasks.Count
        };

        foreach (var task in dueTasks)
        {
            // Idempotence: skip if a reminder already exists for this task in the last threshold period.
            var recentReminderExists = await _db.Notifications
                .AnyAsync(n =>
                    n.TenantId == tenantId &&
                    n.UserId == task.AssignedEmployee!.UserId!.Value &&
                    n.RelatedEntityType == "WorkTask" &&
                    n.RelatedEntityId == task.Id &&
                    n.Title.Contains("vence") &&
                    n.CreatedAt >= DateTime.UtcNow.AddHours(-request.HoursThreshold),
                    cancellationToken);

            if (recentReminderExists)
                continue;

            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = task.AssignedEmployee!.UserId!.Value,
                Title = "Tarea próxima a vencer",
                Message = $"La tarea '{task.Title}' vence el {task.DueAt:yyyy-MM-dd HH:mm}.",
                RelatedEntityType = "WorkTask",
                RelatedEntityId = task.Id
            });

            result.NotificationsSent++;
        }

        if (result.NotificationsSent > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
