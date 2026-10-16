using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One operational state as stored (Fase 5).</summary>
public sealed record WorkflowStateDto(
    Guid Id,
    string StateName,
    bool IsInitial,
    bool CanSubmit);

/// <summary>One allowed move as stored (Fase 5).</summary>
public sealed record WorkflowTransitionDto(
    Guid Id,
    string FromState,
    string ToState,
    string ActionLabel,
    string AllowedRole);

/// <summary>Workflow definition payload (Fase 5).</summary>
public sealed record WorkflowDefinitionDto(
    Guid Id,
    string DocumentType,
    string Name,
    bool IsActive,
    IReadOnlyList<WorkflowStateDto> States,
    IReadOnlyList<WorkflowTransitionDto> Transitions)
{
    public static WorkflowDefinitionDto From(WorkflowDefinition definition) =>
        new(
            definition.Id,
            definition.DocumentType,
            definition.Name,
            definition.IsActive,
            definition.States
                .Select(s => new WorkflowStateDto(s.Id, s.StateName, s.IsInitial, s.CanSubmit))
                .ToList(),
            definition.Transitions
                .Select(t => new WorkflowTransitionDto(t.Id, t.FromState, t.ToState, t.ActionLabel, t.AllowedRole))
                .ToList());
}

/// <summary>One executable move with its caller permission precomputed (ActionBar source).</summary>
public sealed record AvailableTransitionDto(
    string ToState,
    string ActionLabel,
    string AllowedRole,
    bool AllowedForCaller);

/// <summary>Current operational state plus executable moves (ActionBar source).</summary>
public sealed record WorkflowStatusDto(
    Guid DocumentId,
    string DocumentType,
    string CurrentState,
    IReadOnlyList<AvailableTransitionDto> Transitions);
