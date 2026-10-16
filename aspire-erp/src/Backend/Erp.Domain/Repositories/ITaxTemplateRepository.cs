using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the <see cref="TaxTemplate"/> catalog (tax-engine Fase 2).
/// Implemented by Erp.Infrastructure. Tenant isolation is AUTOMATIC (Constitution II.3):
/// implementations query through AppDbContext, whose global filter scopes every read to
/// the current tenant. Templates are additionally company-scoped (like Customers).
/// </summary>
public interface ITaxTemplateRepository
{
    /// <summary>Active templates of one company and category (the UI selector source).</summary>
    Task<IReadOnlyList<TaxTemplate>> GetByCompanyCategoryAsync(
        Guid companyId, string category, CancellationToken cancellationToken = default);

    /// <summary>The template with the given id (lines included), or null outside this tenant.</summary>
    Task<TaxTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the title already exists in this company+category.</summary>
    Task<bool> ExistsTitleAsync(
        Guid companyId, string category, string title, Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a new template with its lines.</summary>
    Task AddAsync(TaxTemplate template, CancellationToken cancellationToken = default);

    /// <summary>Persists an updated template (lines are replaced wholesale).</summary>
    Task UpdateAsync(TaxTemplate template, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes a template and its lines (config, never ledger data).</summary>
    Task DeleteAsync(TaxTemplate template, CancellationToken cancellationToken = default);
}
