using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Work order payload (Task 9.3): header + three-warehouse routing + planned/actual dates.</summary>
public sealed record WorkOrderDto(
    Guid Id,
    Guid CompanyId,
    string OrderNumber,
    Guid ProductionItemId,
    Guid BomId,
    decimal QuantityToProduce,
    decimal ProducedQuantity,
    WorkOrderStatus Status,
    Guid SourceWarehouseId,
    Guid WipWarehouseId,
    Guid TargetWarehouseId,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    DateTimeOffset CreatedAt)
{
    /// <summary>Maps a work order aggregate (header only - the aggregate carries no lines).</summary>
    public static WorkOrderDto Build(WorkOrder order) =>
        new(
            order.Id,
            order.CompanyId,
            order.OrderNumber,
            order.ProductionItemId,
            order.BomId,
            order.QuantityToProduce,
            order.ProducedQuantity,
            order.Status,
            order.SourceWarehouseId,
            order.WipWarehouseId,
            order.TargetWarehouseId,
            order.PlannedStartDate,
            order.PlannedEndDate,
            order.ActualStartDate,
            order.ActualEndDate,
            order.CreatedAt);
}
