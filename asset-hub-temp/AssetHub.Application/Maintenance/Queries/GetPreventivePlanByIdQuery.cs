using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Queries;

public class GetPreventivePlanByIdQuery : IRequest<PreventivePlanDto?>
{
    public Guid Id { get; set; }
}

public class GetPreventivePlanByIdQueryHandler : IRequestHandler<GetPreventivePlanByIdQuery, PreventivePlanDto?>
{
    private readonly ITenantDbContext _db;

    public GetPreventivePlanByIdQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<PreventivePlanDto?> Handle(GetPreventivePlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await _db.PreventivePlans
            .Include(p => p.Asset)
            .Include(p => p.AssetTemplate)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan == null) return null;

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
