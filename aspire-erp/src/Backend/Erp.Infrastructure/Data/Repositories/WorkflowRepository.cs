using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWorkflowRepository"/> (Fase 5).
/// No manual tenant predicates (Constitution II.3 - the global query filter scopes reads).
/// </summary>
public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly AppDbContext _dbContext;

    public WorkflowRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Join an already-open transaction instead of creating a nested one (same DbContext instance).
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<WorkflowDefinition?> GetActiveByDocumentTypeAsync(
        string documentType, CancellationToken cancellationToken = default)
        => _dbContext.WorkflowDefinitions
            .Include(d => d.States)
            .Include(d => d.Transitions)
            .FirstOrDefaultAsync(
                d => d.DocumentType == documentType && d.IsActive, cancellationToken);

    public async Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => await _dbContext.WorkflowDefinitions
            .Include(d => d.States)
            .Include(d => d.Transitions)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);

    public Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.WorkflowDefinitions
            .Include(d => d.States)
            .Include(d => d.Transitions)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsNameAsync(
        string documentType, string name, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => _dbContext.WorkflowDefinitions.AnyAsync(
            d => d.DocumentType == documentType
                && d.Name == name
                && (excludeId == null || d.Id != excludeId),
            cancellationToken);

    public async Task AddAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        await _dbContext.WorkflowDefinitions.AddAsync(definition, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        // Wholesale state/transition replacement through tracked instances only (never
        // Update() on a detached graph: fresh child rows would mark Modified, not Added).
        var persisted = await GetByIdAsync(definition.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow '{definition.Id}' was not found.");

        persisted.Name = definition.Name;
        persisted.IsActive = definition.IsActive;

        _dbContext.Set<WorkflowStateDefinition>().RemoveRange(persisted.States);
        _dbContext.Set<WorkflowTransition>().RemoveRange(persisted.Transitions);

        foreach (var state in definition.States)
        {
            state.Id = Guid.NewGuid();
            state.WorkflowDefinitionId = persisted.Id;
            state.WorkflowDefinition = null;
            await _dbContext.Set<WorkflowStateDefinition>().AddAsync(state, cancellationToken);
        }

        foreach (var transition in definition.Transitions)
        {
            transition.Id = Guid.NewGuid();
            transition.WorkflowDefinitionId = persisted.Id;
            transition.WorkflowDefinition = null;
            await _dbContext.Set<WorkflowTransition>().AddAsync(transition, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _dbContext.WorkflowDefinitions.Remove(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
