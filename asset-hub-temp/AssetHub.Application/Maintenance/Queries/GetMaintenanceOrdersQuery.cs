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

public class GetMaintenanceOrdersQuery : IRequest<GetMaintenanceOrdersResult>
{
    public string? State { get; set; }
    public string? Kind { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? PreventivePlanId { get; set; }
    public Guid? IncidentId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetMaintenanceOrdersResult
{
    public List<MaintenanceOrderSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetMaintenanceOrdersQueryHandler : IRequestHandler<GetMaintenanceOrdersQuery, GetMaintenanceOrdersResult>
{
    private readonly ITenantDbContext _db;

    public GetMaintenanceOrdersQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<GetMaintenanceOrdersResult> Handle(GetMaintenanceOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MaintenanceOrders
            .AsNoTracking()
            .Include(o => o.Asset)
            .Include(o => o.PreventivePlan)
            .Include(o => o.Incident)
            .Include(o => o.AssignedEmployee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.State))
            query = query.Where(o => o.State == request.State);

        if (!string.IsNullOrWhiteSpace(request.Kind))
            query = query.Where(o => o.Kind == request.Kind);

        if (request.AssetId.HasValue)
            query = query.Where(o => o.AssetId == request.AssetId.Value);

        if (request.PreventivePlanId.HasValue)
            query = query.Where(o => o.PreventivePlanId == request.PreventivePlanId.Value);

        if (request.IncidentId.HasValue)
            query = query.Where(o => o.IncidentId == request.IncidentId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.Title.ToLower().Contains(term) ||
                (o.Asset != null && o.Asset.Name.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new MaintenanceOrderSummaryDto
            {
                Id = o.Id,
                Kind = o.Kind,
                State = o.State,
                Title = o.Title,
                CreatedAt = o.CreatedAt,
                ScheduledStart = o.ScheduledStart,
                ScheduledEnd = o.ScheduledEnd,
                CompletedAt = o.CompletedAt,
                LaborCost = o.LaborCost,
                PartsCount = o.Parts.Count,
                AssetId = o.AssetId,
                AssetName = o.Asset != null ? o.Asset.Name : null,
                PreventivePlanId = o.PreventivePlanId,
                PreventivePlanName = o.PreventivePlan != null ? o.PreventivePlan.Name : null,
                IncidentId = o.IncidentId,
                IncidentTitle = o.Incident != null ? o.Incident.Title : null,
                WorkflowTemplateId = o.WorkflowTemplateId != null ? o.WorkflowTemplateId : (o.Incident != null && o.Incident.WorkflowTemplateId != null ? o.Incident.WorkflowTemplateId : (o.PreventivePlan != null ? o.PreventivePlan.WorkflowTemplateId : null)),
                AssignedEmployeeId = o.AssignedEmployeeId,
                AssignedEmployeeName = o.AssignedEmployee != null ? $"{o.AssignedEmployee.FirstName} {o.AssignedEmployee.LastName}" : null
            })
            .ToListAsync(cancellationToken);

        return new GetMaintenanceOrdersResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
