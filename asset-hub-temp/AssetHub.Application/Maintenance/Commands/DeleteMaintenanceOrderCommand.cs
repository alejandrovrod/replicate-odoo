using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class DeleteMaintenanceOrderCommand : IRequest<Unit>
{
    public Guid MaintenanceOrderId { get; set; }
}

public class DeleteMaintenanceOrderCommandHandler : IRequestHandler<DeleteMaintenanceOrderCommand, Unit>
{
    private readonly ITenantDbContext _db;

    public DeleteMaintenanceOrderCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(DeleteMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken);
        if (order == null)
            throw new ArgumentException("Orden de mantenimiento no encontrada");

        if (order.State == MaintenanceOrderStates.Verified)
            throw new InvalidOperationException("No se puede eliminar una orden verificada");

        // Soft delete: also soft-delete child work tasks
        var childTasks = await _db.WorkTasks
            .Where(t => t.MaintenanceOrderId == order.Id && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var task in childTasks)
        {
            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
        }

        order.IsDeleted = true;

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
