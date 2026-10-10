using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Application.Maintenance.Helpers;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class VerifyMaintenanceOrderCommand : IRequest<Unit>
{
    public Guid MaintenanceOrderId { get; set; }
}

public class VerifyMaintenanceOrderCommandHandler : IRequestHandler<VerifyMaintenanceOrderCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMediator _mediator;

    public VerifyMaintenanceOrderCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver, IMediator mediator)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(VerifyMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken);
        if (order == null)
            throw new ArgumentException("Orden de mantenimiento no encontrada");

        if (!MaintenanceOrderStateTransitionValidator.IsValidTransition(order.State, MaintenanceOrderStates.Verified))
            throw new InvalidOperationException(MaintenanceOrderStateTransitionValidator.GetErrorMessage(order.State, MaintenanceOrderStates.Verified));

        order.State = MaintenanceOrderStates.Verified;
        order.CompletedAt = DateTime.UtcNow;

        _db.AssetLifecycleEvents.Add(new AssetLifecycleEvent
        {
            Id = Guid.NewGuid(),
            AssetId = order.AssetId,
            EventType = "MaintenanceIntervention",
            At = order.CompletedAt.Value,
            Notes = $"Verified maintenance order {order.Title}"
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new MaintenanceOrderVerifiedEvent(
            order.Id,
            order.TenantId,
            order.AssetId,
            order.IncidentId
        ), cancellationToken);

        return Unit.Value;
    }
}
