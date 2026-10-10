using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Tasks.Helpers;
using AssetHub.Domain.Tasks;
using Cronos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class EvaluateTaskRecurrencesCommand : IRequest<Unit>
{
    // Usualmente disparado por un Job (sin parámetros)
}

public class EvaluateTaskRecurrencesCommandHandler : IRequestHandler<EvaluateTaskRecurrencesCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public EvaluateTaskRecurrencesCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(EvaluateTaskRecurrencesCommand request, CancellationToken cancellationToken)
    {
        // En un esquema multitenant real con Jobs centralizados, esto se usaría ignorando el filtro o iterando por tenants.
        // Aquí asumimos que el endpoint `/evaluate` es llamado por tenant, o usamos IgnoreQueryFilters().
        
        var now = DateTime.UtcNow;

        var overdueRecurrences = await _db.TaskRecurrences
            .IgnoreQueryFilters()
            .Where(tr => tr.IsActive && tr.NextRunAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var recurrence in overdueRecurrences)
        {
            // Validar si ya generamos una tarea para esta fecha (idempotencia rudimentaria: misma recurrencia y estado todo)
            // Para robustez se debería guardar el "RunDate" generado, pero esto basta para el MVP.
            
            var existingTask = await _db.WorkTasks
                .IgnoreQueryFilters()
                .AnyAsync(t => t.TaskRecurrenceId == recurrence.Id && t.State == "todo", cancellationToken);

            if (!existingTask)
            {
                var cmd = JsonSerializer.Deserialize<CreateWorkTaskCommand>(recurrence.TaskTemplateJson);
                if (cmd != null)
                {
                    var (defaultTaskTypeId, defaultPriorityId) = await TaskCatalogDefaults.EnsureDefaultCatalogsAsync(_db, recurrence.TenantId, cancellationToken);

                    _db.WorkTasks.Add(new WorkTask
                    {
                        Id = Guid.NewGuid(),
                        TenantId = recurrence.TenantId,
                        Title = cmd.Title,
                        Description = cmd.Description,
                        TaskTypeCatalogItemId = cmd.TaskTypeCatalogItemId ?? defaultTaskTypeId,
                        PriorityCatalogItemId = cmd.PriorityCatalogItemId ?? defaultPriorityId,
                        State = "todo",
                        DueAt = cmd.DueAt, // Could be adjusted relative to NextRunAt
                        IsIndependent = cmd.IsIndependent,
                        AssetId = cmd.AssetId,
                        MaintenanceOrderId = cmd.MaintenanceOrderId,
                        IncidentId = cmd.IncidentId,
                        TaskRecurrenceId = recurrence.Id
                    });
                }
            }

            // Calcular siguiente ejecución
            DateTime? nextRun = null;
            if (!string.IsNullOrWhiteSpace(recurrence.CronExpression))
            {
                var expression = CronExpression.Parse(recurrence.CronExpression);
                nextRun = expression.GetNextOccurrence(now);
            }
            else if (recurrence.IntervalDays.HasValue)
            {
                nextRun = now.AddDays(recurrence.IntervalDays.Value);
            }

            if (nextRun.HasValue && (!recurrence.EndsAt.HasValue || nextRun.Value <= recurrence.EndsAt.Value))
            {
                recurrence.NextRunAt = nextRun.Value;
            }
            else
            {
                recurrence.IsActive = false; // Detener recurrencia
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
