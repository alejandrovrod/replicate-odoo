namespace Erp.Domain.Common;

/// <summary>
/// Marks a document aggregate that carries an operational workflow state (workflow Fase 5,
/// gap D1): a free-text state name (e.g. "Admision", "Autorizado") resolved against the
/// tenant's active <c>WorkflowDefinition</c> for the document type. NULL/empty means "no
/// operational state yet" (adopts the flow's initial state on first guard evaluation).
/// </summary>
/// <remarks>
/// This is the OPERATIONAL layer, strictly above the accounting lifecycle: the document's
/// own <c>Status</c> (Draft/Submitted/Cancelled) stays the only truth governing the
/// General Ledger and the Kardex. A workflow can only GATE the Draft → Submitted move
/// (states with <c>CanSubmit</c>); it can never post, unpost or rewrite ledger rows.
/// </remarks>
public interface IWorkflowCarrier
{
    string? WorkflowState { get; set; }
}
