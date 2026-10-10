using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Helpers;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class RejectMaintenanceOrderCommand : IRequest<Unit>
{
    public Guid MaintenanceOrderId { get; set; }
    public List<Guid> ApprovedTaskIds { get; set; } = new();
}

public class RejectMaintenanceOrderCommandHandler : IRequestHandler<RejectMaintenanceOrderCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMediator _mediator;

    public RejectMaintenanceOrderCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver, IMediator mediator)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(RejectMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var order = await _db.MaintenanceOrders
            .Include(o => o.WorkTasks)
            .FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken);

        if (order == null)
            throw new ArgumentException("Orden de mantenimiento no encontrada");

        if (!MaintenanceOrderStateTransitionValidator.IsValidTransition(order.State, MaintenanceOrderStates.Rescheduled))
            throw new InvalidOperationException(MaintenanceOrderStateTransitionValidator.GetErrorMessage(order.State, MaintenanceOrderStates.Rescheduled));

        order.State = MaintenanceOrderStates.Rescheduled;
        order.CompletedAt = null;

        foreach (var task in order.WorkTasks)
        {
            if (!request.ApprovedTaskIds.Contains(task.Id))
            {
                task.State = WorkTaskStates.Rework;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
