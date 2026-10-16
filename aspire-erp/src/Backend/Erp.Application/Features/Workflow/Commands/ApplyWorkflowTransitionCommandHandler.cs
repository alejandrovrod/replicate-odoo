using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Workflow.Commands;

/// <summary>
/// Shared document access for workflow transitions and status reads: loads any of the four
/// carrier aggregates with its company scope, resolves the effective current state
/// (empty adopts the flow's initial state) and persists state moves.
/// </summary>
internal static class WorkflowDocuments
{
    internal sealed record Carrier(Guid CompanyId, IWorkflowCarrier Document);

    internal static async Task<Carrier?> LoadAsync(
        string documentType,
        Guid documentId,
        ISalesOrderRepository salesOrders,
        ISalesInvoiceRepository salesInvoices,
        IPurchaseRepository purchases,
        CancellationToken ct)
    {
        switch (documentType)
        {
            case nameof(SalesOrder):
            {
                var order = await salesOrders.GetOrderByIdAsync(documentId, ct);
                return order is null ? null : new Carrier(order.CompanyId, order);
            }

            case nameof(SalesInvoice):
            {
                var invoice = await salesInvoices.GetByIdAsync(documentId, ct);
                return invoice is null ? null : new Carrier(invoice.CompanyId, invoice);
            }

            case nameof(PurchaseOrder):
            {
                var order = await purchases.GetOrderByIdAsync(documentId, ct);
                return order is null ? null : new Carrier(order.CompanyId, order);
            }

            case nameof(PurchaseInvoice):
            {
                var invoice = await purchases.GetInvoiceByIdAsync(documentId, ct);
                return invoice is null ? null : new Carrier(invoice.CompanyId, invoice);
            }

            default:
                return null;
        }
    }

    internal static string CurrentState(WorkflowDefinition flow, IWorkflowCarrier document)
    {
        if (!string.IsNullOrWhiteSpace(document.WorkflowState))
        {
            return document.WorkflowState.Trim();
        }

        var initial = flow.States.SingleOrDefault(s => s.IsInitial)
            ?? throw new WorkflowValidationException(
                WorkflowErrorCodes.WorkflowNotFound,
                $"Workflow '{flow.Name}' has no initial state.");
        return initial.StateName;
    }

    internal static async Task PersistAsync(
        string documentType,
        Carrier carrier,
        ISalesOrderRepository salesOrders,
        ISalesInvoiceRepository salesInvoices,
        IPurchaseRepository purchases,
        CancellationToken ct)
    {
        switch (carrier.Document)
        {
            case SalesOrder order:
                await salesOrders.UpdateOrderAsync(order, ct);
                break;
            case SalesInvoice invoice:
                await salesInvoices.UpdateAsync(invoice, ct);
                break;
            case PurchaseOrder order:
                await purchases.UpdateOrderAsync(order, ct);
                break;
            case PurchaseInvoice invoice:
                await purchases.UpdateInvoiceAsync(invoice, ct);
                break;
            default:
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.WorkflowNotFound,
                    $"Unknown workflow document type '{documentType}'.");
        }
    }
}

public sealed class ApplyWorkflowTransitionCommandHandler
    : ICommandHandler<ApplyWorkflowTransitionCommand, Result<WorkflowStatusDto>>
{
    private readonly IWorkflowRepository _workflows;
    private readonly ISalesOrderRepository _salesOrders;
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly IPurchaseRepository _purchases;
    private readonly ICurrentUserRoles? _roles;

    public ApplyWorkflowTransitionCommandHandler(
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

    public async Task<Result<WorkflowStatusDto>> HandleAsync(
        ApplyWorkflowTransitionCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentType = WorkflowDocumentTypes.Normalize(command.DocumentType);
            var toState = (command.ToState ?? string.Empty).Trim();
            if (toState.Length == 0)
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.InvalidWorkflowTransition,
                    "A target state is required.");
            }

            var flow = await _workflows.GetActiveByDocumentTypeAsync(documentType, cancellationToken)
                ?? throw new WorkflowValidationException(
                    WorkflowErrorCodes.WorkflowNotFound,
                    $"No active workflow exists for '{documentType}'.");

            var carrier = await WorkflowDocuments.LoadAsync(
                documentType, command.DocumentId, _salesOrders, _salesInvoices, _purchases, cancellationToken)
                ?? throw new WorkflowValidationException(
                    WorkflowErrorCodes.WorkflowNotFound,
                    $"Document '{command.DocumentId}' was not found in this tenant.");

            if (carrier.CompanyId != command.CompanyId)
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.WorkflowNotFound,
                    $"Document '{command.DocumentId}' does not belong to this company.");
            }

            var current = WorkflowDocuments.CurrentState(flow, carrier.Document);
            var transition = flow.Transitions.SingleOrDefault(t =>
                string.Equals(t.FromState, current, StringComparison.Ordinal)
                && string.Equals(t.ToState, toState, StringComparison.Ordinal))
                ?? throw new WorkflowValidationException(
                    WorkflowErrorCodes.InvalidWorkflowTransition,
                    $"No transition exists from '{current}' to '{toState}'.");

            var callerRoles = _roles?.Roles ?? Array.Empty<string>();
            if (!WorkflowValidator.IsRoleAllowed(transition.AllowedRole, callerRoles))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.WorkflowRoleUnauthorized,
                    $"Transition '{transition.ActionLabel}' requires role '{transition.AllowedRole}'.");
            }

            carrier.Document.WorkflowState = toState;
            await WorkflowDocuments.PersistAsync(
                documentType, carrier, _salesOrders, _salesInvoices, _purchases, cancellationToken);

            return Result<WorkflowStatusDto>.Success(new WorkflowStatusDto(
                command.DocumentId,
                documentType,
                toState,
                flow.Transitions
                    .Where(t => string.Equals(t.FromState, toState, StringComparison.Ordinal))
                    .Select(t => new AvailableTransitionDto(
                        t.ToState, t.ActionLabel, t.AllowedRole,
                        WorkflowValidator.IsRoleAllowed(t.AllowedRole, callerRoles)))
                    .ToList()));
        }
        catch (WorkflowValidationException ex)
        {
            return Result<WorkflowStatusDto>.Failure(ex.Code, ex.Message);
        }
    }
}
