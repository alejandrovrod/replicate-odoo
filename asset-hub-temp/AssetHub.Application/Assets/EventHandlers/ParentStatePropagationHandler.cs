using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Assets.Events;
using AssetHub.Application.Assets.Commands;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AssetHub.Domain.AssetTemplates;

namespace AssetHub.Application.Assets.EventHandlers;

public class ParentStatePropagationHandler : INotificationHandler<AssetStateChangedEvent>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMediator _mediator;

    public ParentStatePropagationHandler(ITenantDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task Handle(AssetStateChangedEvent notification, CancellationToken cancellationToken)
    {
        // 1. Load the child asset that changed state
        var childAsset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == notification.AssetId, cancellationToken);

        if (childAsset == null || !childAsset.ParentId.HasValue)
        {
            return; // No parent, nothing to propagate
        }

        // 2. Walk up the ancestor chain. We can't rely on each level publishing a new
        // AssetStateChangedEvent: if an intermediate ancestor doesn't transition, the
        // chain would stop and higher ancestors would never be re-evaluated.
        var visited = new HashSet<Guid> { childAsset.Id }; // Cycle guard
        var parentId = childAsset.ParentId.Value;

        while (visited.Add(parentId))
        {
            var result = await EvaluateParentAsync(parentId, cancellationToken);

            if (result.TransitionTriggered)
            {
                // The command publishes a new AssetStateChangedEvent which continues the chain
                break;
            }

            if (!result.NextParentId.HasValue)
            {
                break;
            }

            parentId = result.NextParentId.Value;
        }
    }

    // Evaluates the ChildStateDependencies of a single parent asset.
    // Returns whether a transition was triggered and the id of the next ancestor up the chain.
    private async Task<(bool TransitionTriggered, Guid? NextParentId)> EvaluateParentAsync(Guid parentId, CancellationToken cancellationToken)
    {
        // Load the Parent and its template
        var parentAsset = await _dbContext.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == parentId, cancellationToken);

        if (parentAsset?.AssetTemplate?.LifecycleStates?.States == null)
        {
            return (false, parentAsset?.ParentId);
        }

        // Get the Parent's current state configuration
        if (!parentAsset.AssetTemplate.LifecycleStates.States.TryGetValue(parentAsset.State, out var currentStateConfig))
        {
            return (false, parentAsset.ParentId); // Parent state not found in config
        }

        if (currentStateConfig.ChildStateDependencies == null || !currentStateConfig.ChildStateDependencies.Any())
        {
            return (false, parentAsset.ParentId); // No dependencies configured for the parent's current state
        }

        // Load all children of this parent to evaluate rules
        var allChildren = await _dbContext.Assets
            .Where(a => a.ParentId == parentAsset.Id && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        // Evaluate the rules in order
        foreach (var rule in currentStateConfig.ChildStateDependencies)
        {
            bool conditionMet = false;

            if (rule.ConditionType.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                // Is there ANY child in one of the required states?
                conditionMet = allChildren.Any(c => rule.ChildStates.Contains(c.State));
            }
            else if (rule.ConditionType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                // Are ALL children in one of the required states?
                conditionMet = allChildren.Count > 0 && allChildren.All(c => rule.ChildStates.Contains(c.State));
            }

            if (conditionMet)
            {
                // Ensure the transition is allowed by the parent's lifecycle
                var allowedTransitions = parentAsset.AssetTemplate.LifecycleStates.Transitions;
                if (allowedTransitions.TryGetValue(parentAsset.State, out var possibleTransitions) &&
                    possibleTransitions.Contains(rule.TargetState))
                {
                    // Trigger the state change
                    // We dispatch a new command so it goes through all normal validations and logs
                    var command = new ChangeAssetEnvironmentStateCommand(
                        parentAsset.Id,
                        rule.TargetState,
                        $"Transición automática propagada por dependencia de estado de sub-activos.",
                        TransitionData: null,
                        IsAutomatedTransition: true
                    );

                    await _mediator.Send(command, cancellationToken);

                    // Stop evaluating after the first rule matches
                    return (true, parentAsset.ParentId);
                }
            }
        }

        return (false, parentAsset.ParentId);
    }
}
