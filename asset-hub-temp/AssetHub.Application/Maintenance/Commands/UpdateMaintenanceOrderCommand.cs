using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class UpdateMaintenanceOrderCommand : IRequest<MaintenanceOrderSummaryDto>
{
    public Guid MaintenanceOrderId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? AssignedEmployeeId { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public bool? RemovePreventivePlan { get; set; }
    public string? PropertiesJson { get; set; }
}

public class UpdateMaintenanceOrderCommandHandler : IRequestHandler<UpdateMaintenanceOrderCommand, MaintenanceOrderSummaryDto>
{
    private readonly ITenantDbContext _db;

    public UpdateMaintenanceOrderCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<MaintenanceOrderSummaryDto> Handle(UpdateMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.MaintenanceOrders
            .Include(o => o.Asset)
            .Include(o => o.PreventivePlan)
            .Include(o => o.Incident)
            .Include(o => o.AssignedEmployee)
            .FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken);
        if (order == null)
            throw new ArgumentException("Orden de mantenimiento no encontrada");

        if (order.State == MaintenanceOrderStates.Verified)
            throw new InvalidOperationException("No se puede actualizar una orden verificada");

        if (request.Title != null)
            order.Title = request.Title;
        if (request.Description != null)
            order.Description = request.Description;
        if (request.AssignedEmployeeId.HasValue)
            order.AssignedEmployeeId = request.AssignedEmployeeId.Value;
        if (request.ScheduledStart.HasValue)
            order.ScheduledStart = request.ScheduledStart.Value;
        if (request.ScheduledEnd.HasValue)
            order.ScheduledEnd = request.ScheduledEnd.Value;
        if (request.RemovePreventivePlan == true)
            order.PreventivePlanId = null;
        if (request.PropertiesJson != null)
        {
            var propertiesChanged = !SemanticJsonEquals(order.PropertiesJson, request.PropertiesJson);
            order.PropertiesJson = request.PropertiesJson;

            if (propertiesChanged)
            {
                await PropagatePropertiesToTasksAsync(order, request.PropertiesJson, cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new MaintenanceOrderSummaryDto
        {
            Id = order.Id,
            Kind = order.Kind,
            State = order.State,
            Title = order.Title,
            CreatedAt = order.CreatedAt,
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
            AssignedEmployeeId = order.AssignedEmployeeId,
            AssignedEmployeeName = order.AssignedEmployee != null ? $"{order.AssignedEmployee.FirstName} {order.AssignedEmployee.LastName}" : null
        };
    }

    /// <summary>
    /// Structural JSON comparison: insensitive to key order, whitespace and formatting.
    /// Invalid or null payloads are compared as raw strings (equal only if both invalid identically).
    /// </summary>
    private static bool SemanticJsonEquals(string? left, string? right)
    {
        if (string.ReferenceEquals(left, right))
            return true;

        if (left == null || right == null)
            return false;

        try
        {
            using var leftDoc = JsonDocument.Parse(left);
            using var rightDoc = JsonDocument.Parse(right);
            return JsonElement.DeepEquals(leftDoc.RootElement, rightDoc.RootElement);
        }
        catch (JsonException)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Cascades dynamic properties from the maintenance order to its non-terminal child work tasks.
    /// Order properties take precedence: task keys are overwritten, unknown keys are preserved.
    /// </summary>
    private async Task PropagatePropertiesToTasksAsync(MaintenanceOrder order, string propertiesJson, CancellationToken cancellationToken)
    {
        var orderProps = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(propertiesJson);
        if (orderProps == null || orderProps.Count == 0)
            return;

        var childTasks = await _db.WorkTasks
            .Where(t => t.MaintenanceOrderId == order.Id)
            .Where(t => !WorkTaskStates.TerminalStates.Contains(t.State))
            .ToListAsync(cancellationToken);

        foreach (var task in childTasks)
        {
            Dictionary<string, JsonElement> taskProps;
            if (string.IsNullOrWhiteSpace(task.PropertiesJson))
            {
                taskProps = new Dictionary<string, JsonElement>();
            }
            else
            {
                try
                {
                    taskProps = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(task.PropertiesJson) ?? new Dictionary<string, JsonElement>();
                }
                catch (JsonException)
                {
                    taskProps = new Dictionary<string, JsonElement>();
                }
            }

            foreach (var kvp in orderProps)
            {
                taskProps[kvp.Key] = kvp.Value;
            }

            task.PropertiesJson = JsonSerializer.Serialize(taskProps);
        }
    }
}
