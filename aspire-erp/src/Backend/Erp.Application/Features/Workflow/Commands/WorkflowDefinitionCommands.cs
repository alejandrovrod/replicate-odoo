using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Workflow.Commands;

/// <summary>Document types carrying an operational workflow (matches typeof(TDoc).Name).</summary>
public static class WorkflowDocumentTypes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(SalesOrder),
        nameof(SalesInvoice),
        nameof(PurchaseOrder),
        nameof(PurchaseInvoice),
    };

    public static string Normalize(string documentType)
    {
        var normalized = (documentType ?? string.Empty).Trim();
        if (!All.Contains(normalized))
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.WorkflowNotFound,
                $"Unknown workflow document type '{documentType}'.");
        }

        return normalized;
    }
}

/// <summary>One state on the wire (Fase 5 catalog).</summary>
public sealed record WorkflowStateInput(string StateName, bool IsInitial, bool CanSubmit);

/// <summary>One transition on the wire (Fase 5 catalog).</summary>
public sealed record WorkflowTransitionInput(
    string FromState,
    string ToState,
    string ActionLabel,
    string AllowedRole = "");

/// <summary>Registers one operational workflow (states + transitions validated as a whole).</summary>
public sealed record CreateWorkflowDefinitionCommand(
    string DocumentType,
    string Name,
    bool IsActive,
    IReadOnlyList<WorkflowStateInput>? States = null,
    IReadOnlyList<WorkflowTransitionInput>? Transitions = null) : ICommand<Result<WorkflowDefinitionDto>>;

/// <summary>Replaces one workflow (document type immutable; states/transitions wholesale).</summary>
public sealed record UpdateWorkflowDefinitionCommand(
    Guid Id,
    string Name,
    bool IsActive,
    IReadOnlyList<WorkflowStateInput>? States = null,
    IReadOnlyList<WorkflowTransitionInput>? Transitions = null) : ICommand<Result<WorkflowDefinitionDto>>;

/// <summary>Hard-deletes one workflow (documents keep their free-text state, unguarded).</summary>
public sealed record DeleteWorkflowDefinitionCommand(Guid Id) : ICommand<Result<bool>>;

/// <summary>Executes one operational move (role-checked, company-scoped).</summary>
public sealed record ApplyWorkflowTransitionCommand(
    string DocumentType,
    Guid CompanyId,
    Guid DocumentId,
    string ToState) : ICommand<Result<WorkflowStatusDto>>;

public sealed class CreateWorkflowDefinitionCommandHandler
    : ICommandHandler<CreateWorkflowDefinitionCommand, Result<WorkflowDefinitionDto>>
{
    private readonly IWorkflowRepository _workflows;

    public CreateWorkflowDefinitionCommandHandler(IWorkflowRepository workflows)
    {
        _workflows = workflows;
    }

    public async Task<Result<WorkflowDefinitionDto>> HandleAsync(
        CreateWorkflowDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentType = WorkflowDocumentTypes.Normalize(command.DocumentType);
            var name = NormalizeName(command.Name);
            var states = NormalizeStates(command.States);
            var transitions = NormalizeTransitions(command.Transitions);

            WorkflowValidator.EnsureValidDefinition(
                states.Select(s => (s.StateName, s.IsInitial, s.CanSubmit)).ToList(),
                transitions.Select(t => (t.FromState, t.ToState, t.ActionLabel, t.AllowedRole)).ToList());

            if (await _workflows.ExistsNameAsync(documentType, name, null, cancellationToken))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.DuplicateWorkflow,
                    $"Workflow '{name}' already exists for '{documentType}'.");
            }

            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                DocumentType = documentType,
                Name = name,
                IsActive = command.IsActive,
                CreatedAt = DateTimeOffset.UtcNow,
                States = states.Select(s => new WorkflowStateDefinition
                {
                    Id = Guid.NewGuid(),
                    StateName = s.StateName,
                    IsInitial = s.IsInitial,
                    CanSubmit = s.CanSubmit,
                }).ToList<WorkflowStateDefinition>(),
                Transitions = transitions.Select(t => new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    FromState = t.FromState,
                    ToState = t.ToState,
                    ActionLabel = t.ActionLabel.Trim(),
                    AllowedRole = (t.AllowedRole ?? string.Empty).Trim(),
                }).ToList<WorkflowTransition>(),
            };

            if (definition.IsActive)
            {
                await DeactivateOthersAsync(documentType, definition.Id, cancellationToken);
            }

            await _workflows.AddAsync(definition, cancellationToken);
            return Result<WorkflowDefinitionDto>.Success(WorkflowDefinitionDto.From(definition));
        }
        catch (WorkflowValidationException ex)
        {
            return Result<WorkflowDefinitionDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task DeactivateOthersAsync(
        string documentType, Guid keepId, CancellationToken ct)
    {
        var all = await _workflows.GetAllAsync(ct);
        foreach (var other in all.Where(d =>
            d.DocumentType == documentType && d.Id != keepId && d.IsActive))
        {
            other.IsActive = false;
            await _workflows.UpdateAsync(other, ct);
        }
    }

    internal static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.DuplicateWorkflow,
                "Workflow name is required and must be at most 100 characters.");
        }

        return name.Trim();
    }

    internal static IReadOnlyList<WorkflowStateInput> NormalizeStates(IReadOnlyList<WorkflowStateInput>? states) =>
        (states ?? Array.Empty<WorkflowStateInput>())
            .Select(s => new WorkflowStateInput(
                (s.StateName ?? string.Empty).Trim(), s.IsInitial, s.CanSubmit))
            .ToList();

    internal static IReadOnlyList<WorkflowTransitionInput> NormalizeTransitions(
        IReadOnlyList<WorkflowTransitionInput>? transitions) =>
        (transitions ?? Array.Empty<WorkflowTransitionInput>())
            .Select(t => new WorkflowTransitionInput(
                (t.FromState ?? string.Empty).Trim(),
                (t.ToState ?? string.Empty).Trim(),
                (t.ActionLabel ?? string.Empty).Trim(),
                (t.AllowedRole ?? string.Empty).Trim()))
            .ToList();
}

public sealed class UpdateWorkflowDefinitionCommandHandler
    : ICommandHandler<UpdateWorkflowDefinitionCommand, Result<WorkflowDefinitionDto>>
{
    private readonly IWorkflowRepository _workflows;

    public UpdateWorkflowDefinitionCommandHandler(IWorkflowRepository workflows)
    {
        _workflows = workflows;
    }

    public async Task<Result<WorkflowDefinitionDto>> HandleAsync(
        UpdateWorkflowDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await _workflows.GetByIdAsync(command.Id, cancellationToken)
                ?? throw new WorkflowValidationException(
                    WorkflowErrorCodes.WorkflowNotFound,
                    $"Workflow '{command.Id}' was not found in this tenant.");

            var name = CreateWorkflowDefinitionCommandHandler.NormalizeName(command.Name);
            var states = CreateWorkflowDefinitionCommandHandler.NormalizeStates(command.States);
            var transitions = CreateWorkflowDefinitionCommandHandler.NormalizeTransitions(command.Transitions);

            WorkflowValidator.EnsureValidDefinition(
                states.Select(s => (s.StateName, s.IsInitial, s.CanSubmit)).ToList(),
                transitions.Select(t => (t.FromState, t.ToState, t.ActionLabel, t.AllowedRole)).ToList());

            if (await _workflows.ExistsNameAsync(definition.DocumentType, name, definition.Id, cancellationToken))
            {
                throw new WorkflowValidationException(
                    WorkflowErrorCodes.DuplicateWorkflow,
                    $"Workflow '{name}' already exists for '{definition.DocumentType}'.");
            }

            definition.Name = name;
            definition.IsActive = command.IsActive;
            definition.States = states.Select(s => new WorkflowStateDefinition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = definition.Id,
                StateName = s.StateName,
                IsInitial = s.IsInitial,
                CanSubmit = s.CanSubmit,
            }).ToList<WorkflowStateDefinition>();
            definition.Transitions = transitions.Select(t => new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = definition.Id,
                FromState = t.FromState,
                ToState = t.ToState,
                ActionLabel = t.ActionLabel.Trim(),
                AllowedRole = (t.AllowedRole ?? string.Empty).Trim(),
            }).ToList<WorkflowTransition>();

            if (definition.IsActive)
            {
                var all = await _workflows.GetAllAsync(cancellationToken);
                foreach (var other in all.Where(d =>
                    d.DocumentType == definition.DocumentType && d.Id != definition.Id && d.IsActive))
                {
                    other.IsActive = false;
                    await _workflows.UpdateAsync(other, cancellationToken);
                }
            }

            await _workflows.UpdateAsync(definition, cancellationToken);
            return Result<WorkflowDefinitionDto>.Success(WorkflowDefinitionDto.From(definition));
        }
        catch (WorkflowValidationException ex)
        {
            return Result<WorkflowDefinitionDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class DeleteWorkflowDefinitionCommandHandler
    : ICommandHandler<DeleteWorkflowDefinitionCommand, Result<bool>>
{
    private readonly IWorkflowRepository _workflows;

    public DeleteWorkflowDefinitionCommandHandler(IWorkflowRepository workflows)
    {
        _workflows = workflows;
    }

    public async Task<Result<bool>> HandleAsync(
        DeleteWorkflowDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var definition = await _workflows.GetByIdAsync(command.Id, cancellationToken);
        if (definition is null)
        {
            return Result<bool>.Failure(
                WorkflowErrorCodes.WorkflowNotFound,
                $"Workflow '{command.Id}' was not found in this tenant.");
        }

        await _workflows.DeleteAsync(definition, cancellationToken);
        return Result<bool>.Success(true);
    }
}
