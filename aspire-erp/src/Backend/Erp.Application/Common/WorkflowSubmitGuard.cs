using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Common;

/// <summary>
/// Operational submit gate as a lifecycle hook (workflow Fase 5, gap D2): plugs into the
/// Fase 1 <see cref="IDocumentLifecycleHook{TDocument}"/> pipeline, so no submit handler
/// learns about workflows - composition over conditionals, one registration per document.
/// </summary>
/// <remarks>
/// <para>Resolved once per document type through the open-generic DI registration
/// (<c>IDocumentLifecycleHook&lt;&gt;</c> → <c>WorkflowSubmitGuard&lt;&gt;</c>), with the
/// document type taken from <c>typeof(TDocument).Name</c> ("SalesOrder", "SalesInvoice",
/// "PurchaseOrder", "PurchaseInvoice").</para>
/// <para>Runs FIRST in the hook pipeline (<see cref="Order"/> = -100): a gated document
/// fails before any other hook or write. When no active flow exists for the type, the
/// guard passes silently (zero behavior change for tenants without workflows).</para>
/// <para>Documents with an empty state adopt the flow's initial state (existing drafts are
/// never bricked by activating a flow); only <c>CanSubmit</c> states proceed.</para>
/// </remarks>
public sealed class WorkflowSubmitGuard<TDocument> : IDocumentLifecycleHook<TDocument>
    where TDocument : class, IWorkflowCarrier
{
    private readonly IWorkflowRepository _workflows;

    public WorkflowSubmitGuard(IWorkflowRepository workflows)
    {
        _workflows = workflows;
    }

    /// <summary>Guards run before every other hook (fail fast on gated documents).</summary>
    public int Order => -100;

    public async Task OnBeforeSubmitAsync(TDocument document, CancellationToken ct)
    {
        var flow = await _workflows.GetActiveByDocumentTypeAsync(typeof(TDocument).Name, ct);
        if (flow is null)
        {
            return;
        }

        string state;
        if (string.IsNullOrWhiteSpace(document.WorkflowState))
        {
            var initial = flow.States.SingleOrDefault(s => s.IsInitial)
                ?? throw new WorkflowValidationException(
                    WorkflowErrorCodes.WorkflowSubmitNotAllowed,
                    $"Workflow '{flow.Name}' has no initial state: no document can submit under it.");
            state = initial.StateName;
            document.WorkflowState = state;
        }
        else
        {
            state = document.WorkflowState.Trim();
        }

        var authorized = flow.States.Any(s =>
            string.Equals(s.StateName, state, StringComparison.Ordinal) && s.CanSubmit);

        if (!authorized)
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.WorkflowSubmitNotAllowed,
                $"Document sits in operational state '{state}': only {DescribeSubmittable(flow)} may submit.");
        }
    }

    public Task OnAfterSubmitAsync(TDocument document, CancellationToken ct) =>
        Task.CompletedTask;

    public Task OnBeforeCancelAsync(TDocument document, CancellationToken ct) =>
        Task.CompletedTask;

    public Task OnAfterCancelAsync(TDocument document, CancellationToken ct) =>
        Task.CompletedTask;

    private static string DescribeSubmittable(WorkflowDefinition flow)
    {
        var states = flow.States.Where(s => s.CanSubmit).Select(s => $"'{s.StateName}'").ToList();
        return states.Count switch
        {
            0 => "no state (misconfigured flow)",
            1 => $"state {states[0]}",
            _ => $"states {string.Join(", ", states)}",
        };
    }
}
