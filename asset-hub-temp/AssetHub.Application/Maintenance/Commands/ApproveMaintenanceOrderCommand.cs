using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Application.Maintenance.Helpers;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class ApproveMaintenanceOrderCommand : IRequest<Unit>
{
    public Guid MaintenanceOrderId { get; set; }
}

public class ApproveMaintenanceOrderCommandHandler : IRequestHandler<ApproveMaintenanceOrderCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;

    public ApproveMaintenanceOrderCommandHandler(ITenantDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(ApproveMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken);
        if (order == null)
            throw new ArgumentException("Orden de mantenimiento no encontrada");

        if (!MaintenanceOrderStateTransitionValidator.IsValidTransition(order.State, MaintenanceOrderStates.Approved))
            throw new InvalidOperationException(MaintenanceOrderStateTransitionValidator.GetErrorMessage(order.State, MaintenanceOrderStates.Approved));

        order.State = MaintenanceOrderStates.Approved;
        await _db.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new MaintenanceOrderApprovedEvent(
            order.Id,
            order.TenantId,
            order.AssetId,
            order.IncidentId
        ), cancellationToken);

        return Unit.Value;
    }
}
