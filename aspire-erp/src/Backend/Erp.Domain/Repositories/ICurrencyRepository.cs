using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the global Currency catalog (RM-09).
/// Implemented by Erp.Infrastructure.Data.Repositories.CurrencyRepository.
/// </summary>
/// <remarks>
/// Currencies are GLOBAL (shared across tenants): the <see cref="Currency"/> entity carries no
/// TenantId, so no tenant filter applies - every read sees the full ISO catalog.
/// </remarks>
public interface ICurrencyRepository
{
    /// <summary>Persists a new currency (codes are unique globally).</summary>
    Task AddAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>True when the ISO code already exists (case-insensitive).</summary>
    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>The currency with the given id, or null.</summary>
    Task<Currency?> GetByIdAsync(Guid currencyId, CancellationToken cancellationToken = default);

    /// <summary>The ACTIVE currency with the given ISO code (case-insensitive), or null.</summary>
    Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every currency ordered by Code; optionally only the active ones.</summary>
    Task<IReadOnlyList<Currency>> GetAllAsync(bool onlyActive, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing currency (optimistic concurrency via RowVersion).</summary>
    Task UpdateAsync(Currency currency, CancellationToken cancellationToken = default);
}
