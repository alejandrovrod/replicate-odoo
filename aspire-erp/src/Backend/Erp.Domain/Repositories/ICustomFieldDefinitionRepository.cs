using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the <see cref="CustomFieldDefinition"/> catalog
/// (extensibility-engine Fase 1). Implemented by Erp.Infrastructure. Tenant isolation is
/// AUTOMATIC (Constitution II.3): implementations query through AppDbContext, whose global
/// filter scopes every read to the current tenant.
/// </summary>
public interface ICustomFieldDefinitionRepository
{
    /// <summary>Active definitions for one root entity (validation + UI rendering).</summary>
    Task<IReadOnlyList<CustomFieldDefinition>> GetActiveByEntityAsync(
        string entityName, CancellationToken cancellationToken = default);

    /// <summary>The definition with the given id, or null when it does not exist in this tenant.</summary>
    Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when an active (EntityName, FieldName) pair already exists in this tenant.</summary>
    Task<bool> ExistsAsync(
        string entityName, string fieldName, Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a new definition.</summary>
    Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Persists an updated definition.</summary>
    Task UpdateAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes a definition (catalog row, never ledger data).</summary>
    Task DeleteAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default);
}
