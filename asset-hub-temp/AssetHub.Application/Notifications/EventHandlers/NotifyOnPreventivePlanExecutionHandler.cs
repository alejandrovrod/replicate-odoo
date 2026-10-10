using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Application.Notifications.Commands;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Notifications.EventHandlers;

public class NotifyOnPreventivePlanExecutionHandler : INotificationHandler<PreventivePlanExecutedEvent>
{
    private readonly IMediator _mediator;
    private readonly ITenantDbContext _db;

    public NotifyOnPreventivePlanExecutionHandler(IMediator mediator, ITenantDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public async Task Handle(PreventivePlanExecutedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.GeneratedItems.Count == 0)
            return;

        var recipientIds = new System.Collections.Generic.HashSet<System.Guid>();

        // Notify the assigned employee if present
        if (notification.AssignedEmployeeId.HasValue)
        {
            var employee = await _db.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == notification.AssignedEmployeeId.Value, cancellationToken);

            if (employee?.UserId != null)
            {
                recipientIds.Add(employee.UserId.Value);
            }
        }

        // Notify team members if assigned to a team
        if (notification.AssignedTeamId.HasValue)
        {
            var teamMembers = await _db.TeamMembers
                .IgnoreQueryFilters()
                .Where(tm => tm.TeamId == notification.AssignedTeamId.Value)
                .ToListAsync(cancellationToken);

            foreach (var member in teamMembers)
            {
                var employee = await _db.Employees
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == member.EmployeeId, cancellationToken);

                if (employee?.UserId != null)
                {
                    recipientIds.Add(employee.UserId.Value);
                }
            }
        }

        if (recipientIds.Count == 0)
            return;

        var itemsSummary = string.Join(", ",
            notification.GeneratedItems
                .GroupBy(i => i.EntityType)
                .Select(g => $"{g.Count()} {g.Key}(s)"));

        var assetNames = string.Join(", ",
            notification.GeneratedItems
                .Select(i => i.AssetName ?? i.AssetId.ToString())
                .Distinct()
                .Take(3));

        var title = $"Preventive plan executed: {notification.PlanName}";
        var message = $"Generated {itemsSummary} for assets: {assetNames}.";

        foreach (var userId in recipientIds)
        {
            await _mediator.Send(new CreateNotificationCommand
            {
                TenantId = notification.TenantId,
                UserId = userId,
                Title = title,
                Message = message,
                RelatedEntityType = "PreventivePlan",
                RelatedEntityId = notification.PlanId
            }, cancellationToken);
        }
    }
}
