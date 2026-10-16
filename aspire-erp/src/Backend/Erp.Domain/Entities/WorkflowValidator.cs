using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure definition-shape rules for operational workflows (Fase 5, T1). Transition
/// execution (current-state match, role check) lives in the application layer, which owns
/// the caller roles and the document reads.
/// </summary>
public static class WorkflowValidator
{
    /// <summary>
    /// Definition rules: at least one state, exactly one initial, at least one CanSubmit,
    /// unique state names, and every transition endpoint naming an existing state with
    /// FromState != ToState and no duplicate (From, To) pairs.
    /// </summary>
    /// <exception cref="WorkflowValidationException">An invariant was violated.</exception>
    public static void EnsureValidDefinition(
        IReadOnlyList<(string StateName, bool IsInitial, bool CanSubmit)> states,
        IReadOnlyList<(string FromState, string ToState, string ActionLabel, string AllowedRole)> transitions)
    {
        if (states.Count == 0)
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.InvalidWorkflowTransition,
                "A workflow needs at least one state.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in states)
        {
            var name = (state.StateName ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 100)
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.InvalidWorkflowTransition,
                    "State names are required and must be at most 100 characters.");
            }

            if (!names.Add(name))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.DuplicateWorkflow,
                    $"Duplicate workflow state '{name}'.");
            }
        }

        if (states.Count(s => s.IsInitial) != 1)
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.InvalidWorkflowTransition,
                "A workflow needs exactly one initial state.");
        }

        if (!states.Any(s => s.CanSubmit))
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.InvalidWorkflowTransition,
                "A workflow needs at least one submit-authorized (CanSubmit) state.");
        }

        var pairs = new HashSet<(string From, string To)>();
        for (var i = 0; i < transitions.Count; i++)
        {
            var transition = transitions[i];
            var from = (transition.FromState ?? string.Empty).Trim();
            var to = (transition.ToState ?? string.Empty).Trim();

            if (!names.Contains(from) || !names.Contains(to))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.InvalidWorkflowTransition,
                    $"Transition {i + 1} references an unknown state ('{from}' -> '{to}').");
            }

            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.InvalidWorkflowTransition,
                    $"Transition {i + 1} is a self-loop ('{from}'): states advance, never stall.");
            }

            if (!pairs.Add((from, to)))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.DuplicateWorkflow,
                    $"Duplicate transition '{from}' -> '{to}'.");
            }

            if (string.IsNullOrWhiteSpace(transition.ActionLabel))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.InvalidWorkflowTransition,
                    $"Transition {i + 1} ('{from}' -> '{to}') needs an action label for the UI bar.");
            }
        }
    }

    /// <summary>Role gate: empty AllowedRole means any document writer may execute.</summary>
    public static bool IsRoleAllowed(string allowedRole, IReadOnlyCollection<string> callerRoles) =>
        string.IsNullOrWhiteSpace(allowedRole)
        || callerRoles.Any(r => string.Equals(r.Trim(), allowedRole.Trim(), StringComparison.OrdinalIgnoreCase));
}
