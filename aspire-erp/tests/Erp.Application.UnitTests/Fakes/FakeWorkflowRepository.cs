using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="IWorkflowRepository"/> over a seeded definition set.</summary>
public sealed class FakeWorkflowRepository : IWorkflowRepository
{
    private readonly List<WorkflowDefinition> _definitions = new();

    public IReadOnlyList<WorkflowDefinition> Definitions => _definitions;

    public void Seed(params WorkflowDefinition[] definitions) => _definitions.AddRange(definitions);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        => operation(cancellationToken);

    public Task<WorkflowDefinition?> GetActiveByDocumentTypeAsync(
        string documentType, CancellationToken cancellationToken = default)
        => Task.FromResult(_definitions.FirstOrDefault(d =>
            d.DocumentType == documentType && d.IsActive));

    public Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkflowDefinition>>(_definitions.ToList());

    public Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_definitions.FirstOrDefault(d => d.Id == id));

    public Task<bool> ExistsNameAsync(
        string documentType, string name, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_definitions.Any(d =>
            d.DocumentType == documentType
            && d.Name == name
            && (excludeId == null || d.Id != excludeId)));

    public Task AddAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions.Add(definition);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions.Remove(definition);
        return Task.CompletedTask;
    }
}
