namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for operational workflows (Fase 5). They flow
/// Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where controllers map them to RFC 7807
/// ProblemDetails - mirroring <see cref="StockErrorCodes"/>.
/// </summary>
public static class WorkflowErrorCodes
{
    /// <summary>Submit attempted from a state without CanSubmit (WF-PRESET-G1).</summary>
    public const string WorkflowSubmitNotAllowed = "workflow_submit_not_allowed";

    /// <summary>No transition exists from the current state to the requested one.</summary>
    public const string InvalidWorkflowTransition = "invalid_workflow_transition";

    /// <summary>The caller's roles do not include the transition's AllowedRole.</summary>
    public const string WorkflowRoleUnauthorized = "workflow_role_unauthorized";

    public const string WorkflowNotFound = "workflow_not_found";

    public const string DuplicateWorkflow = "duplicate_workflow";

    /// <summary>The requested preset key does not exist.</summary>
    public const string PresetNotFound = "preset_not_found";
}
