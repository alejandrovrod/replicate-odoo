using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Queries;

public record GetMaintenanceOrderByIdQuery(Guid Id) : IRequest<MaintenanceOrderDetailDto?>;

public class GetMaintenanceOrderByIdQueryHandler : IRequestHandler<GetMaintenanceOrderByIdQuery, MaintenanceOrderDetailDto?>
{
    private readonly ITenantDbContext _db;

    public GetMaintenanceOrderByIdQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<MaintenanceOrderDetailDto?> Handle(GetMaintenanceOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _db.MaintenanceOrders
            .AsNoTracking()
            .Include(o => o.Asset)
            .Include(o => o.PreventivePlan)
            .Include(o => o.Incident)
            .Include(o => o.AssignedEmployee)
            .Include(o => o.Parts)
                .ThenInclude(p => p.CatalogItem)
                    .ThenInclude(ci => ci!.Translations)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (order == null)
            return null;

        var tasks = await _db.WorkTasks
            .AsNoTracking()
            .Include(t => t.AssignedEmployee)
            .Where(t => t.MaintenanceOrderId == order.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new MaintenanceOrderTaskSummaryDto
            {
                Id = t.Id,
                Title = t.Title,
                State = t.State,
                AssignedEmployeeId = t.AssignedEmployeeId,
                AssignedEmployeeName = t.AssignedEmployee != null ? $"{t.AssignedEmployee.FirstName} {t.AssignedEmployee.LastName}" : null
            })
            .ToListAsync(cancellationToken);

        var parts = order.Parts
            .Select(p => new MaintenanceOrderPartDto
            {
                Id = p.Id,
                CatalogItemId = p.CatalogItemId,
                CatalogItemLabel = p.CatalogItem != null
                    ? p.CatalogItem.Translations
                        .Where(tr => tr.Locale == "es")
                        .Select(tr => tr.Label)
                        .FirstOrDefault()
                    : null,
                Quantity = p.Quantity,
                UnitCost = p.UnitCost
            })
            .ToArray();

        return new MaintenanceOrderDetailDto
        {
            Id = order.Id,
            Kind = order.Kind,
            State = order.State,
            Title = order.Title,
            CreatedAt = order.CreatedAt,
            Description = order.Description,
            ScheduledStart = order.ScheduledStart,
            ScheduledEnd = order.ScheduledEnd,
            CompletedAt = order.CompletedAt,
            LaborCost = order.LaborCost,
            PartsCount = order.Parts.Count,
            AssetId = order.AssetId,
            AssetName = order.Asset?.Name,
            PreventivePlanId = order.PreventivePlanId,
            PreventivePlanName = order.PreventivePlan?.Name,
            IncidentId = order.IncidentId,
            IncidentTitle = order.Incident?.Title,
            WorkflowTemplateId = order.WorkflowTemplateId ?? order.Incident?.WorkflowTemplateId ?? order.PreventivePlan?.WorkflowTemplateId,
            AssignedEmployeeId = order.AssignedEmployeeId,
            AssignedEmployeeName = order.AssignedEmployee != null ? $"{order.AssignedEmployee.FirstName} {order.AssignedEmployee.LastName}" : null,
            PropertiesJson = order.PropertiesJson,
            Parts = parts,
            Tasks = tasks.ToArray()
        };
    }
}
