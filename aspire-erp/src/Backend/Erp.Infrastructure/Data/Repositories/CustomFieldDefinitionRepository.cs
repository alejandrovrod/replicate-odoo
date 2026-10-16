using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICustomFieldDefinitionRepository"/>
/// (extensibility-engine Fase 1). No manual tenant predicates (Constitution II.3 - the
/// global query filter scopes every read); the unique index backs the raced-duplicate rule.
/// </summary>
public sealed class CustomFieldDefinitionRepository : ICustomFieldDefinitionRepository
{
    private readonly AppDbContext _dbContext;

    public CustomFieldDefinitionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetActiveByEntityAsync(
        string entityName, CancellationToken cancellationToken = default)
        => await _dbContext.CustomFieldDefinitions
            .Where(d => d.EntityName == entityName && d.IsActive)
            .OrderBy(d => d.FieldName)
            .ToListAsync(cancellationToken);

    public Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(
        string entityName, string fieldName, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => _dbContext.CustomFieldDefinitions.AnyAsync(
            d => d.EntityName == entityName
                && d.FieldName == fieldName
                && (excludeId == null || d.Id != excludeId),
            cancellationToken);

    public async Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        await _dbContext.CustomFieldDefinitions.AddAsync(definition, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        _dbContext.CustomFieldDefinitions.Update(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        _dbContext.CustomFieldDefinitions.Remove(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
