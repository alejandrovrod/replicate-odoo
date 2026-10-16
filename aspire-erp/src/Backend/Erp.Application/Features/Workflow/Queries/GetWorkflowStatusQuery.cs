using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Application.Features.Workflow.Commands;

namespace Erp.Application.Features.Workflow.Queries;

/// <summary>Current operational state plus executable moves (ActionBar source).</summary>
public sealed record GetWorkflowStatusQuery(
    string DocumentType,
    Guid CompanyId,
    Guid DocumentId) : IQuery<WorkflowStatusDto?>;

public sealed class GetWorkflowStatusQueryHandler
    : IQueryHandler<GetWorkflowStatusQuery, WorkflowStatusDto?>
{
    private readonly IWorkflowRepository _workflows;
    private readonly ISalesOrderRepository _salesOrders;
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly IPurchaseRepository _purchases;
    private readonly ICurrentUserRoles? _roles;

    public GetWorkflowStatusQueryHandler(
        IWorkflowRepository workflows,
        ISalesOrderRepository salesOrders,
        ISalesInvoiceRepository salesInvoices,
        IPurchaseRepository purchases,
        ICurrentUserRoles? roles = null)
    {
        _workflows = workflows;
        _salesOrders = salesOrders;
        _salesInvoices = salesInvoices;
        _purchases = purchases;
        _roles = roles;
    }

    public async Task<WorkflowStatusDto?> HandleAsync(
        GetWorkflowStatusQuery query, CancellationToken cancellationToken = default)
    {
        var documentType = WorkflowDocumentTypes.Normalize(query.DocumentType);
        var flow = await _workflows.GetActiveByDocumentTypeAsync(documentType, cancellationToken);
        if (flow is null)
        {
            return null;
        }

        var carrier = await WorkflowDocuments.LoadAsync(
            documentType, query.DocumentId, _salesOrders, _salesInvoices, _purchases, cancellationToken);
        if (carrier is null || carrier.CompanyId != query.CompanyId)
        {
            return null;
        }

        var current = WorkflowDocuments.CurrentState(flow, carrier.Document);
        var callerRoles = _roles?.Roles ?? Array.Empty<string>();

        return new WorkflowStatusDto(
            query.DocumentId,
            documentType,
            current,
            flow.Transitions
                .Where(t => string.Equals(t.FromState, current, StringComparison.Ordinal))
                .Select(t => new AvailableTransitionDto(
                    t.ToState, t.ActionLabel, t.AllowedRole,
                    WorkflowValidator.IsRoleAllowed(t.AllowedRole, callerRoles)))
                .ToList());
    }
}
