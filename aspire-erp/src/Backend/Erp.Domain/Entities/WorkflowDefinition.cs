using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Operational state machine of one document type for one tenant (workflow Fase 5, gap D1):
/// named states plus the allowed transitions between them, with the role that may execute
/// each transition. Tenant-wide (like custom fields): one active definition per document
/// type governs every company of the tenant.
/// </summary>
/// <remarks>
/// No <c>AggregateRoot&lt;T&gt;</c> base exists in this codebase (all aggregates implement
/// <see cref="ITenantEntity"/> directly), so this entity follows the same shape.
/// </remarks>
public class WorkflowDefinition : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Document type name, e.g. "SalesOrder" (matches typeof(TDoc).Name).</summary>
    public string DocumentType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Only one definition per document type is active at a time (enforced on write).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<WorkflowStateDefinition> States { get; set; } = new List<WorkflowStateDefinition>();

    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
}

/// <summary>One operational state: initial entry point and/or submit-authorized.</summary>
public class WorkflowStateDefinition
{
    public Guid Id { get; set; }

    public Guid WorkflowDefinitionId { get; set; }

    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public string StateName { get; set; } = string.Empty;

    /// <summary>Entry state adopted by documents with no operational state yet.</summary>
    public bool IsInitial { get; set; }

    /// <summary>Only documents sitting in a CanSubmit state may move to Submitted.</summary>
    public bool CanSubmit { get; set; }
}

/// <summary>One allowed move between states, with its action label and executing role.</summary>
public class WorkflowTransition
{
    public Guid Id { get; set; }

    public Guid WorkflowDefinitionId { get; set; }

    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public string FromState { get; set; } = string.Empty;

    public string ToState { get; set; } = string.Empty;

    /// <summary>Button label rendered by the WorkflowActionBar (e.g. "Aprobar").</summary>
    public string ActionLabel { get; set; } = string.Empty;

    /// <summary>
    /// Role that may execute the transition (matches the JWT role claim). Empty means any
    /// authenticated document writer (the permission system stays the security boundary).
    /// </summary>
    public string AllowedRole { get; set; } = string.Empty;
}
