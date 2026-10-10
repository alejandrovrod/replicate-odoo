using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Tasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Queries;

public record GetWorkTaskByIdQuery(Guid Id) : IRequest<WorkTaskDetailDto?>;

public class GetWorkTaskByIdQueryHandler : IRequestHandler<GetWorkTaskByIdQuery, WorkTaskDetailDto?>
{
    private readonly ITenantDbContext _db;

    public GetWorkTaskByIdQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<WorkTaskDetailDto?> Handle(GetWorkTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var task = await _db.WorkTasks
            .AsNoTracking()
            .Include(t => t.Asset)
            .Include(t => t.Incident)
                .ThenInclude(i => i!.Asset)
            .Include(t => t.MaintenanceOrder)
                .ThenInclude(mo => mo!.Asset)
            .Include(t => t.PreventivePlan)
                .ThenInclude(pp => pp!.Asset)
            .Include(t => t.TaskRecurrence)
            .Include(t => t.TaskTypeCatalogItem)
                .ThenInclude(ci => ci!.Translations)
            .Include(t => t.PriorityCatalogItem)
                .ThenInclude(ci => ci!.Translations)
            .Include(t => t.AssignedEmployee)
            .Include(t => t.AssignedTeam)
            .Include(t => t.StatusHistory)
            .Include(t => t.TaskComments)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (task == null)
        {
            return null;
        }

        var taskTypeLabel = task.TaskTypeCatalogItem != null
            ? task.TaskTypeCatalogItem.Translations
                .Where(tr => tr.Locale == "es")
                .Select(tr => tr.Label)
                .FirstOrDefault()
            : null;

        var priorityLabel = task.PriorityCatalogItem != null
            ? task.PriorityCatalogItem.Translations
                .Where(tr => tr.Locale == "es")
                .Select(tr => tr.Label)
                .FirstOrDefault()
            : null;

        return new WorkTaskDetailDto
        {
            Id = task.Id,
            TenantId = task.TenantId,
            Title = task.Title,
            Description = task.Description,
            State = task.State,
            TaskTypeCatalogItemId = task.TaskTypeCatalogItemId,
            TaskTypeLabel = taskTypeLabel,
            PriorityCatalogItemId = task.PriorityCatalogItemId,
            PriorityLabel = priorityLabel,
            DueAt = task.DueAt,
            CreatedAt = task.CreatedAt,
            AssetId = task.AssetId ?? task.MaintenanceOrder?.AssetId ?? task.PreventivePlan?.AssetId ?? task.Incident?.AssetId,
            AssetName = task.Asset != null ? task.Asset.Name : (task.MaintenanceOrder?.Asset?.Name ?? task.PreventivePlan?.Asset?.Name ?? task.Incident?.Asset?.Name),
            IncidentId = task.IncidentId,
            IncidentTitle = task.Incident != null ? task.Incident.Title : null,
            WorkflowTemplateId = task.MaintenanceOrder?.WorkflowTemplateId ?? task.Incident?.WorkflowTemplateId ?? task.PreventivePlan?.WorkflowTemplateId,
            MaintenanceOrderId = task.MaintenanceOrderId,
            MaintenanceOrderTitle = task.MaintenanceOrder != null ? task.MaintenanceOrder.Title : null,
            MaintenanceOrderState = task.MaintenanceOrder != null ? task.MaintenanceOrder.State : null,
            PreventivePlanId = task.PreventivePlanId,
            PreventivePlanName = task.PreventivePlan != null ? task.PreventivePlan.Name : null,
            TaskRecurrenceId = task.TaskRecurrenceId,
            TaskRecurrenceName = null,
            AssignedEmployeeId = task.AssignedEmployeeId,
            AssignedEmployeeName = task.AssignedEmployee != null ? $"{task.AssignedEmployee.FirstName} {task.AssignedEmployee.LastName}" : null,
            AssignedTeamId = task.AssignedTeamId,
            AssignedTeamName = task.AssignedTeam != null ? task.AssignedTeam.Name : null,
            PropertiesJson = task.PropertiesJson,
            IsIndependent = task.IsIndependent,
            History = task.StatusHistory
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new TaskStatusHistoryDto
                {
                    Id = h.Id,
                    FromState = h.FromState,
                    ToState = h.ToState,
                    ChangedAt = h.ChangedAt,
                    ChangedByName = null
                })
                .ToList(),
            Comments = task.TaskComments
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new TaskCommentDto
                {
                    Id = c.Id,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    CreatedByName = null
                })
                .ToList()
        };
    }
}
