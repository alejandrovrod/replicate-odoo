using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class TogglePreventivePlanActiveCommand : IRequest<PreventivePlanDto>
{
    public Guid Id { get; set; }
}

public class TogglePreventivePlanActiveCommandHandler : IRequestHandler<TogglePreventivePlanActiveCommand, PreventivePlanDto>
{
    private readonly ITenantDbContext _db;

    public TogglePreventivePlanActiveCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<PreventivePlanDto> Handle(TogglePreventivePlanActiveCommand request, CancellationToken cancellationToken)
    {
        var plan = await _db.PreventivePlans
            .Include(p => p.Asset)
            .Include(p => p.AssetTemplate)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan == null)
        {
            throw new ArgumentException($"Plan preventivo '{request.Id}' no encontrado.");
        }

        plan.IsActive = !plan.IsActive;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(plan);
    }

    private static PreventivePlanDto MapToDto(Domain.Maintenance.PreventivePlan plan)
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
