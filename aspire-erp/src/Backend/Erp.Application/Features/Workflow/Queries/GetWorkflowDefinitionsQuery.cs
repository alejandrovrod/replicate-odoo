using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Workflow.Queries;

/// <summary>Every workflow definition of the tenant (admin list).</summary>
public sealed record GetWorkflowDefinitionsQuery : IQuery<IReadOnlyList<WorkflowDefinitionDto>>;

/// <summary>One workflow definition (or null).</summary>
public sealed record GetWorkflowDefinitionByIdQuery(Guid Id) : IQuery<WorkflowDefinitionDto?>;

public sealed class GetWorkflowDefinitionsQueryHandler
    : IQueryHandler<GetWorkflowDefinitionsQuery, IReadOnlyList<WorkflowDefinitionDto>>
{
    private readonly IWorkflowRepository _workflows;

    public GetWorkflowDefinitionsQueryHandler(IWorkflowRepository workflows)
    {
        _workflows = workflows;
    }

    public async Task<IReadOnlyList<WorkflowDefinitionDto>> HandleAsync(
        GetWorkflowDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        var definitions = await _workflows.GetAllAsync(cancellationToken);
        return definitions.Select(WorkflowDefinitionDto.From).ToList();
    }
}

public sealed class GetWorkflowDefinitionByIdQueryHandler
    : IQueryHandler<GetWorkflowDefinitionByIdQuery, WorkflowDefinitionDto?>
{
    private readonly IWorkflowRepository _workflows;

    public GetWorkflowDefinitionByIdQueryHandler(IWorkflowRepository workflows)
    {
        _workflows = workflows;
    }

    public async Task<WorkflowDefinitionDto?> HandleAsync(
        GetWorkflowDefinitionByIdQuery query, CancellationToken cancellationToken = default)
    {
        var definition = await _workflows.GetByIdAsync(query.Id, cancellationToken);
        return definition is null ? null : WorkflowDefinitionDto.From(definition);
    }
}
