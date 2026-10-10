using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using AssetHub.Domain.Maintenance;
using Cronos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class UpdatePreventivePlanCommand : IRequest<PreventivePlanDto>
{
    public Guid Id { get; set; }
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

public class UpdatePreventivePlanCommandHandler : IRequestHandler<UpdatePreventivePlanCommand, PreventivePlanDto>
{
    private readonly ITenantDbContext _db;

    public UpdatePreventivePlanCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<PreventivePlanDto> Handle(UpdatePreventivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _db.PreventivePlans
            .Include(p => p.Asset)
            .Include(p => p.AssetTemplate)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan == null)
        {
            throw new ArgumentException($"Plan preventivo '{request.Id}' no encontrado.");
        }

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
            throw new ArgumentException($"Tipo de entidad generada inv�lido: {request.GeneratedEntityType}");
        }

        if (request.DueDateOffsetDays < 0)
        {
            throw new ArgumentException("Los d�as de offset para el vencimiento deben ser mayores o iguales a 0.");
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
            throw new ArgumentException($"Expresi�n Cron inv�lida: {ex.Message}");
        }

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.AssetTemplateId = request.AssetTemplateId;
        plan.AssetId = request.AssetId;
        plan.WorkflowTemplateId = request.WorkflowTemplateId;
        plan.GeneratedEntityType = request.GeneratedEntityType;
        plan.CronExpression = request.CronExpression;
        plan.DueDateOffsetDays = request.DueDateOffsetDays;
        plan.ConditionRuleJson = request.ConditionRuleJson;
        plan.DefaultAssignedEmployeeId = request.DefaultAssignedEmployeeId;
        plan.DefaultAssignedTeamId = request.DefaultAssignedTeamId;
        plan.AutoAssign = request.AutoAssign;
        plan.EndsAt = request.EndsAt;
        plan.NextRunAt = nextRunAt;

        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(plan);
    }

    private static PreventivePlanDto MapToDto(PreventivePlan plan)
    {
        return new PreventivePlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            TargetType = plan.AssetId.HasValue ? "Asset" : "AssetTemplate",
            AssetId = plan.AssetId,
            AssetName = plan.Asset?.Name,
            AssetTemplateId = plan.AssetTemplateId,
            AssetTemplateName = plan.AssetTemplate?.Name,
            WorkflowTemplateId = plan.WorkflowTemplateId,
            GeneratedEntityType = plan.GeneratedEntityType,
            CronExpression = plan.CronExpression,
            DueDateOffsetDays = plan.DueDateOffsetDays,
            ConditionRuleJson = plan.ConditionRuleJson,
            AutoAssign = plan.AutoAssign,
            DefaultAssignedEmployeeId = plan.DefaultAssignedEmployeeId,
            DefaultAssignedTeamId = plan.DefaultAssignedTeamId,
            NextRunAt = plan.NextRunAt,
            LastRunAt = plan.LastRunAt,
            EndsAt = plan.EndsAt,
            IsActive = plan.IsActive
        };
    }
}
