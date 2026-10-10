using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class RemoveMaintenancePartCommand : IRequest<Unit>
{
    public Guid MaintenanceOrderId { get; set; }
    public Guid PartId { get; set; }
}

public class RemoveMaintenancePartCommandHandler : IRequestHandler<RemoveMaintenancePartCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly IInventoryPostingService _inventoryPostingService;

    public RemoveMaintenancePartCommandHandler(ITenantDbContext db, IInventoryPostingService inventoryPostingService)
    {
        _db = db;
        _inventoryPostingService = inventoryPostingService;
    }

    public async Task<Unit> Handle(RemoveMaintenancePartCommand request, CancellationToken cancellationToken)
    {
        var part = await _db.MaintenanceParts
            .FirstOrDefaultAsync(p => p.Id == request.PartId && p.MaintenanceOrderId == request.MaintenanceOrderId, cancellationToken);

        if (part == null)
            throw new ArgumentException("Parte no encontrada");

        var order = await _db.MaintenanceOrders
            .FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken);

        if (order?.State == MaintenanceOrderStates.Verified)
            throw new InvalidOperationException("No se pueden eliminar partes de una orden verificada");

        if (part.InventoryTransactionId.HasValue && part.WarehouseId.HasValue)
        {
            // Reverse the consumption by posting a positive adjustment
            await _inventoryPostingService.PostTransactionAsync(
                warehouseId: part.WarehouseId.Value,
                catalogItemId: part.CatalogItemId,
                quantity: part.Quantity, // Reversing, so positive back into stock
                unitCost: part.UnitCost,
                type: "Reversal",
                reason: $"Reversal for removed part on Order {order?.Id.ToString()}",
                idempotencyKey: $"Reverse_{part.Id}",
                maintenanceOrderId: order?.Id,
                cancellationToken: cancellationToken
            );
        }

        _db.MaintenanceParts.Remove(part);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
