using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Queries;

public class GetPreventivePlansQuery : IRequest<GetPreventivePlansResult>
{
    public Guid? AssetId { get; set; }
    public Guid? TemplateId { get; set; }
    public bool? Active { get; set; }
}

public class GetPreventivePlansResult
{
    public List<PreventivePlanDto> Items { get; set; } = new();
}

public class GetPreventivePlansQueryHandler : IRequestHandler<GetPreventivePlansQuery, GetPreventivePlansResult>
{
    private readonly ITenantDbContext _db;

    public GetPreventivePlansQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<GetPreventivePlansResult> Handle(GetPreventivePlansQuery request, CancellationToken cancellationToken)
    {
        var query = _db.PreventivePlans
            .Include(p => p.Asset)
            .Include(p => p.AssetTemplate)
            .AsQueryable();

        if (request.AssetId.HasValue)
        {
            query = query.Where(p => p.AssetId == request.AssetId.Value);
        }

        if (request.TemplateId.HasValue)
        {
            query = query.Where(p => p.AssetTemplateId == request.TemplateId.Value);
        }

        if (request.Active.HasValue)
        {
            query = query.Where(p => p.IsActive == request.Active.Value);
        }

        var items = await query
            .OrderBy(p => p.Name)
            .Select(p => new PreventivePlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                TargetType = p.AssetId.HasValue ? "Asset" : "AssetTemplate",
                AssetId = p.AssetId,
                AssetName = p.Asset != null ? p.Asset.Name : null,
                AssetTemplateId = p.AssetTemplateId,
                AssetTemplateName = p.AssetTemplate != null ? p.AssetTemplate.Name : null,
                WorkflowTemplateId = p.WorkflowTemplateId,
                GeneratedEntityType = p.GeneratedEntityType,
                CronExpression = p.CronExpression,
                DueDateOffsetDays = p.DueDateOffsetDays,
                ConditionRuleJson = p.ConditionRuleJson,
                AutoAssign = p.AutoAssign,
                DefaultAssignedEmployeeId = p.DefaultAssignedEmployeeId,
                DefaultAssignedTeamId = p.DefaultAssignedTeamId,
                NextRunAt = p.NextRunAt,
                LastRunAt = p.LastRunAt,
                EndsAt = p.EndsAt,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);

        return new GetPreventivePlansResult { Items = items };
    }
}
