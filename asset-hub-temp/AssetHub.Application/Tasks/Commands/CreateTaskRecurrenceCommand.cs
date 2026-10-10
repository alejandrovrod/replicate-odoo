using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tasks;
using Cronos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class CreateTaskRecurrenceCommand : IRequest<Guid>
{
    public string? CronExpression { get; set; }
    public int? IntervalDays { get; set; }
    
    public DateTime? EndsAt { get; set; }
    
    public string TaskTemplateJson { get; set; } = string.Empty;
}

public class CreateTaskRecurrenceCommandHandler : IRequestHandler<CreateTaskRecurrenceCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public CreateTaskRecurrenceCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateTaskRecurrenceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        if (string.IsNullOrWhiteSpace(request.CronExpression) && !request.IntervalDays.HasValue)
            throw new ArgumentException("Debe proporcionar una expresión Cron o un intervalo en días");

        DateTime nextRunAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.CronExpression))
        {
            try
            {
                var expression = CronExpression.Parse(request.CronExpression);
                var next = expression.GetNextOccurrence(DateTime.UtcNow);
                if (!next.HasValue)
                    throw new ArgumentException("Expresión Cron inválida: no hay ocurrencias futuras");
                nextRunAt = next.Value;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Expresión Cron inválida: {ex.Message}");
            }
        }
        else if (request.IntervalDays.HasValue)
        {
            nextRunAt = DateTime.UtcNow.AddDays(request.IntervalDays.Value);
        }

        var recurrence = new TaskRecurrence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            CronExpression = request.CronExpression,
            IntervalDays = request.IntervalDays,
            NextRunAt = nextRunAt,
            EndsAt = request.EndsAt,
            TaskTemplateJson = request.TaskTemplateJson,
            IsActive = true
        };

        _db.TaskRecurrences.Add(recurrence);
        await _db.SaveChangesAsync(cancellationToken);

        return recurrence.Id;
    }
}
