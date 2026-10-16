using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ICustomFieldDefinitionRepository"/> over a seeded definition set.</summary>
public sealed class FakeCustomFieldDefinitionRepository : ICustomFieldDefinitionRepository
{
    private readonly List<CustomFieldDefinition> _definitions = new();

    public IReadOnlyList<CustomFieldDefinition> Definitions => _definitions;

    public void Seed(params CustomFieldDefinition[] definitions) => _definitions.AddRange(definitions);

    public Task<IReadOnlyList<CustomFieldDefinition>> GetActiveByEntityAsync(
        string entityName, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CustomFieldDefinition>>(
            _definitions.Where(d => d.EntityName == entityName && d.IsActive).ToList());

    public Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_definitions.FirstOrDefault(d => d.Id == id));

    public Task<bool> ExistsAsync(
        string entityName, string fieldName, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_definitions.Any(d =>
            d.EntityName == entityName
            && d.FieldName == fieldName
            && (excludeId == null || d.Id != excludeId)));

    public Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions.Add(definition);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions.Remove(definition);
        return Task.CompletedTask;
    }
}
