using System;
using System.Collections.Generic;
using AssetHub.Domain.Tasks;

namespace AssetHub.Application.Tasks.Helpers;

public static class TaskStateTransitionValidator
{
    private static readonly HashSet<(string From, string To)> AllowedTransitions = new()
    {
        (WorkTaskStates.Todo, WorkTaskStates.InProgress),
        (WorkTaskStates.Todo, WorkTaskStates.Cancelled),
        (WorkTaskStates.Rework, WorkTaskStates.InProgress),
        (WorkTaskStates.Rework, WorkTaskStates.Cancelled),
        (WorkTaskStates.InProgress, WorkTaskStates.Done),
        (WorkTaskStates.InProgress, WorkTaskStates.Cancelled),
    };

    public static readonly HashSet<string> TerminalStates = WorkTaskStates.TerminalStates;

    public static bool IsValidTransition(string? fromState, string toState)
    {
        if (string.IsNullOrWhiteSpace(fromState))
        {
            return toState == WorkTaskStates.Todo;
        }

        if (TerminalStates.Contains(fromState))
        {
            return false;
        }

        return AllowedTransitions.Contains((fromState, toState));
    }

    public static string GetErrorMessage(string? fromState, string toState)
    {
        if (string.IsNullOrWhiteSpace(fromState))
        {
            return $"Una nueva tarea debe iniciar en estado '{WorkTaskStates.Todo}'.";
        }

        if (TerminalStates.Contains(fromState))
        {
            return $"No se puede cambiar el estado desde '{fromState}' porque es un estado terminal.";
        }

        return $"Transición de estado inválida de '{fromState}' a '{toState}'.";
    }
}
