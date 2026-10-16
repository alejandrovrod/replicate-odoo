using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Extensibility.Queries;

/// <summary>Active definitions for one root entity (powers validation + the UI renderer).</summary>
public sealed record GetCustomFieldDefinitionsQuery(string EntityName)
    : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>;

public sealed class GetCustomFieldDefinitionsQueryHandler
    : IQueryHandler<GetCustomFieldDefinitionsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
    private readonly ICustomFieldDefinitionRepository _definitions;

    public GetCustomFieldDefinitionsQueryHandler(ICustomFieldDefinitionRepository definitions)
    {
        _definitions = definitions;
    }

    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> HandleAsync(
        GetCustomFieldDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await _definitions.GetActiveByEntityAsync(
            (query.EntityName ?? string.Empty).Trim(), cancellationToken);
        return rows
            .Where(d => d.IsActive)
            .OrderBy(d => d.FieldName, StringComparer.Ordinal)
            .Select(CustomFieldDefinitionDto.From)
            .ToList();
    }
}
