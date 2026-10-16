using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITaxTemplateRepository"/> (tax-engine Fase 2).
/// No manual tenant predicates (Constitution II.3 - the global query filter scopes every
/// read); company scoping is a business predicate, like Customers.
/// </summary>
public sealed class TaxTemplateRepository : ITaxTemplateRepository
{
    private readonly AppDbContext _dbContext;

    public TaxTemplateRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TaxTemplate>> GetByCompanyCategoryAsync(
        Guid companyId, string category, CancellationToken cancellationToken = default)
        => await _dbContext.TaxTemplates
            .Where(t => t.CompanyId == companyId && t.Category == category)
            .Include(t => t.Taxes)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Title)
            .ToListAsync(cancellationToken);

    public Task<TaxTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.TaxTemplates
            .Include(t => t.Taxes)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExistsTitleAsync(
        Guid companyId, string category, string title, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => _dbContext.TaxTemplates.AnyAsync(
            t => t.CompanyId == companyId
                && t.Category == category
                && t.Title == title
                && (excludeId == null || t.Id != excludeId),
            cancellationToken);

    public async Task AddAsync(TaxTemplate template, CancellationToken cancellationToken = default)
    {
        await _dbContext.TaxTemplates.AddAsync(template, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaxTemplate template, CancellationToken cancellationToken = default)
    {
        // Wholesale line replacement through tracked instances only (never Update() on a
        // detached graph: it would mark fresh lines Modified instead of Added). The header
        // scalars are copied; dropped lines are deleted; incoming lines are always fresh
        // inserts (the handler assigns new ids per update).
        var persisted = await GetByIdAsync(template.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Tax template '{template.Id}' was not found.");

        persisted.Title = template.Title;
        persisted.Category = template.Category;
        persisted.IsDefault = template.IsDefault;

        _dbContext.Set<TaxTemplateItem>().RemoveRange(persisted.Taxes);
        foreach (var line in template.Taxes)
        {
            line.Id = Guid.NewGuid();
            line.TaxTemplateId = persisted.Id;
            line.TaxTemplate = null;
            await _dbContext.Set<TaxTemplateItem>().AddAsync(line, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TaxTemplate template, CancellationToken cancellationToken = default)
    {
        _dbContext.TaxTemplates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
