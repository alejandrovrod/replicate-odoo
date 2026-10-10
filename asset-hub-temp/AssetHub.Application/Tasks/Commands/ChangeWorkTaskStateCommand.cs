using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Tasks.Events;
using AssetHub.Application.Tasks.Helpers;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class ChangeWorkTaskStateCommand : IRequest<Unit>
{
    public Guid WorkTaskId { get; set; }

    [JsonPropertyName("state")]
    public string NewState { get; set; } = string.Empty;
}

public class ChangeWorkTaskStateCommandHandler : IRequestHandler<ChangeWorkTaskStateCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMediator _mediator;

    public ChangeWorkTaskStateCommandHandler(ITenantDbContext db, ICurrentUser currentUser, ITenantResolver tenantResolver, IMediator mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantResolver = tenantResolver;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(ChangeWorkTaskStateCommand request, CancellationToken cancellationToken)
    {
        var task = await _db.WorkTasks.FirstOrDefaultAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (task == null)
            throw new ArgumentException("Tarea no encontrada");

        var oldState = task.State;
        var newState = request.NewState;

        if (oldState == newState)
            return Unit.Value;

        if (!TaskStateTransitionValidator.IsValidTransition(oldState, newState))
        {
            throw new InvalidOperationException(TaskStateTransitionValidator.GetErrorMessage(oldState, newState));
        }

        if (oldState == WorkTaskStates.Todo && newState == WorkTaskStates.InProgress)
        {
            task.StartedAt = DateTime.UtcNow;
        }
        else if (newState == WorkTaskStates.Done)
        {
            task.CompletedAt = DateTime.UtcNow;
        }

        task.State = newState;

        _db.TaskStatusHistories.Add(new TaskStatusHistory
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantResolver.GetCurrentTenantId()!.Value,
            WorkTaskId = task.Id,
            FromState = oldState,
            ToState = newState,
            ChangedByUserId = _currentUser.Id,
            ChangedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new WorkTaskStateChangedEvent(
            task.Id,
            task.TenantId,
            oldState,
            newState,
            task.AssetId,
            task.IncidentId,
            task.MaintenanceOrderId,
            task.PreventivePlanId
        ), cancellationToken);

        return Unit.Value;
    }
}
