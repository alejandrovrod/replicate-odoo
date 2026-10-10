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

public class ScheduleMaintenanceOrderCommand : IRequest<Unit>
{
    public Guid MaintenanceOrderId { get; set; }
    public Guid? AssignedEmployeeId { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
}

public class ScheduleMaintenanceOrderCommandHandler : IRequestHandler<ScheduleMaintenanceOrderCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;

    public ScheduleMaintenanceOrderCommandHandler(ITenantDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(ScheduleMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.ScheduledStart.HasValue && request.ScheduledEnd.HasValue && request.ScheduledEnd < request.ScheduledStart)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la fecha de inicio");

        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken);
        if (order == null)
            throw new ArgumentException("Orden de mantenimiento no encontrada");

        bool changedState = order.State != MaintenanceOrderStates.Scheduled;
        if (changedState && !MaintenanceOrderStateTransitionValidator.IsValidTransition(order.State, MaintenanceOrderStates.Scheduled))
            throw new InvalidOperationException(MaintenanceOrderStateTransitionValidator.GetErrorMessage(order.State, MaintenanceOrderStates.Scheduled));

        if (changedState && (!request.ScheduledStart.HasValue || !request.ScheduledEnd.HasValue))
        {
            if (!order.ScheduledStart.HasValue || !order.ScheduledEnd.HasValue)
            {
                throw new InvalidOperationException("Las fechas de programación (inicio y fin) son requeridas para programar la orden");
            }
        }

        if (request.AssignedEmployeeId.HasValue)
            order.AssignedEmployeeId = request.AssignedEmployeeId.Value;
        if (request.ScheduledStart.HasValue)
            order.ScheduledStart = request.ScheduledStart.Value;
        if (request.ScheduledEnd.HasValue)
            order.ScheduledEnd = request.ScheduledEnd.Value;

        if (changedState)
            order.State = MaintenanceOrderStates.Scheduled;

        await _db.SaveChangesAsync(cancellationToken);

        if (changedState)
        {
            await _mediator.Publish(new MaintenanceOrderScheduledEvent(
                order.Id,
                order.TenantId,
                order.AssetId,
                order.IncidentId
            ), cancellationToken);
        }

        return Unit.Value;
    }
}
