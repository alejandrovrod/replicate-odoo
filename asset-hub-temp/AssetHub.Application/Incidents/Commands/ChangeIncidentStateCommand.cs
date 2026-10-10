using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Incidents.Events;
using AssetHub.Application.Incidents.Helpers;
using AssetHub.Domain.Incidents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Incidents.Commands;

public class ChangeIncidentStateCommand : IRequest<Unit>
{
    public Guid IncidentId { get; set; }
    public string TargetState { get; set; } = string.Empty;
    public string? PropertiesJson { get; set; }
    public bool ForceTransition { get; set; } = false;
}

public class ChangeIncidentStateCommandHandler : IRequestHandler<ChangeIncidentStateCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;

    public ChangeIncidentStateCommandHandler(ITenantDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(ChangeIncidentStateCommand request, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents
            .Include(i => i.WorkflowTemplate)
            .FirstOrDefaultAsync(i => i.Id == request.IncidentId, cancellationToken);
            
        if (incident == null)
            throw new ArgumentException("Incident not found");

        string fromState = incident.State;
        bool isTerminal = false;

        if (request.TargetState != incident.State)
        {
            if (incident.WorkflowTemplate != null && incident.WorkflowTemplate.LifecycleStates != null)
            {
                var config = incident.WorkflowTemplate.LifecycleStates;
                
                if (config.States != null && !config.States.ContainsKey(request.TargetState))
                {
                    throw new Exception($"Invalid state transition: state {request.TargetState} is not defined in template");
                }
                
                if (!request.ForceTransition && config.Transitions != null && config.Transitions.TryGetValue(incident.State, out var allowedTransitions))
                {
                    if (allowedTransitions != null && !allowedTransitions.Contains(request.TargetState))
                    {
                        throw new Exception($"Transition from {incident.State} to {request.TargetState} is not allowed");
                    }
                }
                
                if (config.States != null && config.States.TryGetValue(request.TargetState, out var stateConfig))
                {
                    isTerminal = stateConfig.IsTerminal;
                }
            }
            else
            {
                // Regla RN-11.2: reported->triaged->assigned->in_progress->resolved->closed. cancelled from anywhere.
                if (!request.ForceTransition && request.TargetState != IncidentStates.Cancelled)
                {
                    bool isValid = false;
                    switch (incident.State)
                    {
                        case IncidentStates.Reported:
                            if (request.TargetState == IncidentStates.Triaged) isValid = true;
                            break;
                        case IncidentStates.Triaged:
                            if (request.TargetState == IncidentStates.Assigned) isValid = true;
                            break;
                        case IncidentStates.Assigned:
                            if (request.TargetState == IncidentStates.InProgress) isValid = true;
                            break;
                        case IncidentStates.InProgress:
                            if (request.TargetState == IncidentStates.Resolved) isValid = true;
                            break;
                        case IncidentStates.Resolved:
                            if (request.TargetState == IncidentStates.Closed) isValid = true;
                            break;
                    }

                    if (!isValid)
                        throw new InvalidOperationException($"Invalid transition from {incident.State} to {request.TargetState}");
                }
                
                isTerminal = IncidentStates.TerminalStates.Contains(request.TargetState);
            }
        }
        else 
        {
            // Even if same state, calculate isTerminal
            if (incident.WorkflowTemplate != null && incident.WorkflowTemplate.LifecycleStates != null && 
                incident.WorkflowTemplate.LifecycleStates.States != null && 
                incident.WorkflowTemplate.LifecycleStates.States.TryGetValue(request.TargetState, out var stateConfig))
            {
                isTerminal = stateConfig.IsTerminal;
            }
            else
            {
                isTerminal = IncidentStates.TerminalStates.Contains(request.TargetState);
            }
        }

        if (isTerminal && !await IncidentClosingGuard.CanCloseAsync(_db, incident.Id, incident.TenantId, cancellationToken))
        {
            throw new InvalidOperationException("No se puede cerrar el incidente mientras tenga órdenes de mantenimiento activas o tareas abiertas.");
        }

        incident.State = request.TargetState;
        
        if (!string.IsNullOrWhiteSpace(request.PropertiesJson))
        {
            try
            {
                var existingNode = !string.IsNullOrWhiteSpace(incident.PropertiesJson) 
                    ? JsonNode.Parse(incident.PropertiesJson) as JsonObject 
                    : null;
                var updateNode = JsonNode.Parse(request.PropertiesJson) as JsonObject;

                if (existingNode != null && updateNode != null)
                {
                    foreach (var kvp in updateNode)
                    {
                        existingNode[kvp.Key] = kvp.Value?.DeepClone();
                    }
                    incident.PropertiesJson = existingNode.ToJsonString();
                }
                else
                {
                    incident.PropertiesJson = request.PropertiesJson;
                }
            }
            catch
            {
                incident.PropertiesJson = request.PropertiesJson;
            }
        }

        if (request.TargetState == IncidentStates.Resolved)
            incident.ResolvedAt = DateTime.UtcNow;
            
        if (request.TargetState == IncidentStates.Closed || isTerminal)
            incident.ClosedAt = DateTime.UtcNow;

        if (fromState != request.TargetState)
        {
            _db.IncidentLifecycleEvents.Add(new AssetHub.Domain.Incidents.IncidentLifecycleEvent
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                EventType = "cambio de estado",
                FromState = fromState,
                ToState = request.TargetState,
                Notes = $"Transición a {request.TargetState}",
                PropertiesJson = request.PropertiesJson ?? "{}",
                At = DateTime.UtcNow,
                UserId = Guid.Empty // Sistema por ahora
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        
        if (fromState != request.TargetState)
        {
            await _mediator.Publish(new IncidentStateChangedEvent(
                incident.Id,
                incident.AssetId,
                fromState,
                request.TargetState,
                isTerminal
            ), cancellationToken);

            bool isAssignedState = false;

            if (incident.WorkflowTemplate != null && incident.WorkflowTemplate.LifecycleStates != null && 
                incident.WorkflowTemplate.LifecycleStates.States != null && 
                incident.WorkflowTemplate.LifecycleStates.States.TryGetValue(request.TargetState, out var targetConfig))
            {
                if (targetConfig.AssociatedModule == "maintenance" || 
                    targetConfig.AssociatedModule == "orders" || 
                    targetConfig.AssociatedModule == "maintenance_orders" ||
                    targetConfig.AssociatedModule == "work_orders")
                {
                    isAssignedState = true;
                }
            }
            else if (request.TargetState == IncidentStates.Assigned)
            {
                isAssignedState = true;
            }

            if (isAssignedState)
            {
                await _mediator.Publish(new IncidentAssignedEvent(
                    incident.Id,
                    incident.AssetId,
                    incident.TenantId
                ), cancellationToken);
            }

            if (isTerminal)
            {
                await _mediator.Publish(new IncidentClosedEvent(
                    incident.Id,
                    incident.AssetId,
                    incident.TenantId,
                    request.TargetState
                ), cancellationToken);
            }
        }
        
        return Unit.Value;
    }
}
