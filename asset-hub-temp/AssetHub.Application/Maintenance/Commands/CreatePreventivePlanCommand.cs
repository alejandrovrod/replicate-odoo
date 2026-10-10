using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Maintenance;
using MediatR;
using Cronos;

namespace AssetHub.Application.Maintenance.Commands;

public class CreatePreventivePlanCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssetTemplateId { get; set; }
    public Guid? AssetId { get; set; }

    public Guid? WorkflowTemplateId { get; set; }

    public string GeneratedEntityType { get; set; } = PreventivePlanConstants.GeneratedEntityTypeWorkTask;
    public string CronExpression { get; set; } = string.Empty;

    public int DueDateOffsetDays { get; set; } = 7;
    public string? ConditionRuleJson { get; set; }

    public Guid? DefaultAssignedEmployeeId { get; set; }
    public Guid? DefaultAssignedTeamId { get; set; }
    public bool AutoAssign { get; set; }

    public DateTime? EndsAt { get; set; }
}

public class CreatePreventivePlanCommandHandler : IRequestHandler<CreatePreventivePlanCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public CreatePreventivePlanCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreatePreventivePlanCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        if ((request.AssetTemplateId.HasValue && request.AssetId.HasValue) ||
            (!request.AssetTemplateId.HasValue && !request.AssetId.HasValue))
        {
            throw new ArgumentException("Un plan preventivo debe dirigirse a una plantilla o a un activo, pero no a ambos.");
        }

        if (!new[] {
            PreventivePlanConstants.GeneratedEntityTypeWorkTask,
            PreventivePlanConstants.GeneratedEntityTypeMaintenanceOrder,
            PreventivePlanConstants.GeneratedEntityTypeBoth
        }.Contains(request.GeneratedEntityType))
        {
            throw new ArgumentException($"Tipo de entidad generada inválido: {request.GeneratedEntityType}");
        }

        if (request.DueDateOffsetDays < 0)
        {
            throw new ArgumentException("Los días de offset para el vencimiento deben ser mayores o iguales a 0.");
        }

        DateTime? nextRunAt;
        try
        {
            var expression = CronExpression.Parse(request.CronExpression);
            var tz = AssetHub.Application.Common.Time.TimeHelper.GetMexicoCityTimeZone();
            nextRunAt = expression.GetNextOccurrence(DateTime.UtcNow, tz);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Expresión Cron inválida: {ex.Message}");
        }

        var plan = new PreventivePlan
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            Description = request.Description,
            AssetTemplateId = request.AssetTemplateId,
            AssetId = request.AssetId,
            WorkflowTemplateId = request.WorkflowTemplateId,
            GeneratedEntityType = request.GeneratedEntityType,
            CronExpression = request.CronExpression,
            DueDateOffsetDays = request.DueDateOffsetDays,
            ConditionRuleJson = request.ConditionRuleJson,
            DefaultAssignedEmployeeId = request.DefaultAssignedEmployeeId,
            DefaultAssignedTeamId = request.DefaultAssignedTeamId,
            AutoAssign = request.AutoAssign,
            NextRunAt = nextRunAt,
            EndsAt = request.EndsAt,
            IsActive = true
        };

        _db.PreventivePlans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);

        return plan.Id;
    }
}
