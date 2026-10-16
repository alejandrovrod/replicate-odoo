using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for operational workflow definitions (Fase 5). Implemented by
/// Erp.Infrastructure. Tenant isolation is AUTOMATIC (Constitution II.3).
/// </summary>
public interface IWorkflowRepository
{
    /// <summary>The active definition for one document type, or null when no flow governs it.</summary>
    Task<WorkflowDefinition?> GetActiveByDocumentTypeAsync(
        string documentType, CancellationToken cancellationToken = default);

    /// <summary>Every definition of the tenant (admin list).</summary>
    Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The definition with the given id, or null outside this tenant.</summary>
    Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when another definition already uses the name for the document type.</summary>
    Task<bool> ExistsNameAsync(
        string documentType, string name, Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a new definition with states and transitions.</summary>
    Task AddAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Persists an updated definition (states/transitions replaced wholesale).</summary>
    Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes a definition (documents keep their free-text state, unguarded).</summary>
    Task DeleteAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Runs the operation inside ONE database transaction (preset atomicity).</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
