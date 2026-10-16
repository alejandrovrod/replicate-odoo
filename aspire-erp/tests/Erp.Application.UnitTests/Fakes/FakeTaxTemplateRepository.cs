using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ITaxTemplateRepository"/> over a seeded template set.</summary>
public sealed class FakeTaxTemplateRepository : ITaxTemplateRepository
{
    private readonly List<TaxTemplate> _templates = new();

    public IReadOnlyList<TaxTemplate> Templates => _templates;

    public void Seed(params TaxTemplate[] templates) => _templates.AddRange(templates);

    public Task<IReadOnlyList<TaxTemplate>> GetByCompanyCategoryAsync(
        Guid companyId, string category, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaxTemplate>>(
            _templates.Where(t => t.CompanyId == companyId && t.Category == category).ToList());

    public Task<TaxTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));

    public Task<bool> ExistsTitleAsync(
        Guid companyId, string category, string title, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_templates.Any(t =>
            t.CompanyId == companyId
            && t.Category == category
            && t.Title == title
            && (excludeId == null || t.Id != excludeId)));

    public Task AddAsync(TaxTemplate template, CancellationToken cancellationToken = default)
    {
        _templates.Add(template);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TaxTemplate template, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(TaxTemplate template, CancellationToken cancellationToken = default)
    {
        _templates.Remove(template);
        return Task.CompletedTask;
    }
}
