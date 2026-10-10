using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class CreateMaintenanceOrderCommand : IRequest<Guid>
{
    public string Kind { get; set; } = MaintenanceOrderKinds.Corrective;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetId { get; set; }

    public Guid? PreventivePlanId { get; set; }
    public Guid? WorkflowTemplateId { get; set; }
    public Guid? IncidentId { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    
    public bool GenerateChecklistTasks { get; set; } = false;
}

public class CreateMaintenanceOrderCommandHandler : IRequestHandler<CreateMaintenanceOrderCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMediator _mediator;

    public CreateMaintenanceOrderCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver, IMediator mediator)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(CreateMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var asset = await _db.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);
            
        if (asset == null)
            throw new ArgumentException("Activo no encontrado");

        if (string.IsNullOrWhiteSpace(request.Kind))
            throw new ArgumentException("El tipo de orden no puede estar vacío.");

        if (request.PreventivePlanId.HasValue && request.IncidentId.HasValue)
            throw new ArgumentException("La orden no puede tener tanto un plan preventivo como un incidente asociados");

        var order = new MaintenanceOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Kind = request.Kind,
            State = MaintenanceOrderStates.Draft,
            Title = request.Title,
            Description = request.Description,
            AssetId = request.AssetId,
            PreventivePlanId = request.PreventivePlanId,
            IncidentId = request.IncidentId,
            WorkflowTemplateId = request.WorkflowTemplateId,
            PropertiesJson = request.PropertiesJson
        };

        _db.MaintenanceOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new AssetHub.Application.Maintenance.Events.MaintenanceOrderCreatedEvent(
            order.Id,
            order.TenantId,
            order.AssetId,
            order.PropertiesJson
        ), cancellationToken);

        if (request.GenerateChecklistTasks && !string.IsNullOrWhiteSpace(asset.AssetTemplate?.MaintenanceChecklist))
        {
            var (typeId, priorityId) = await AssetHub.Application.Maintenance.Helpers.PreventivePlanCatalogDefaults.EnsureDefaultCatalogsAsync(_db, tenantId.Value, cancellationToken);
            
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<ChecklistSchema>(asset.AssetTemplate.MaintenanceChecklist, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed?.Tasks != null && parsed.Tasks.Count > 0)
                {
                    foreach (var ct in parsed.Tasks)
                    {
                        var task = new AssetHub.Domain.Tasks.WorkTask
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId.Value,
                            Title = ct.Title ?? "Tarea de checklist",
                            Description = !string.IsNullOrWhiteSpace(ct.Frequency) 
                                ? $"Frecuencia sugerida: {ct.Frequency}\n{ct.Description}" 
                                : ct.Description,
                            State = "todo",
                            TaskTypeCatalogItemId = typeId,
                            PriorityCatalogItemId = priorityId,
                            DueAt = DateTime.UtcNow.AddDays(7), // default due date
                            AssetId = asset.Id,
                            MaintenanceOrderId = order.Id,
                            IsIndependent = false,
                            PropertiesJson = asset.PropertiesJson
                        };

                        _db.WorkTasks.Add(task);
                        
                        await _mediator.Publish(new AssetHub.Application.Tasks.Events.WorkTaskCreatedEvent(
                            task.Id,
                            task.TenantId,
                            task.AssetId,
                            task.PropertiesJson
                        ), cancellationToken);
                    }
                    
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        return order.Id;
    }
    
    private class ChecklistSchema
    {
        public System.Collections.Generic.List<ChecklistTaskSchema>? Tasks { get; set; }
    }
    
    private class ChecklistTaskSchema
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Frequency { get; set; }
    }
}
