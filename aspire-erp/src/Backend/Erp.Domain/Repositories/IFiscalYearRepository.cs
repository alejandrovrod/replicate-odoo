using System.Data;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the <see cref="FiscalYear"/> aggregate (plan.md §1, R-13).
/// The overlap guard runs inside the serializable create transaction (spec FC-04): date-range
/// overlap cannot be expressed as a single CHECK, so the unique index plus the serializable
/// scope makes concurrent overlapping creates serialize.
/// </summary>
public interface IFiscalYearRepository
{
    Task<FiscalYear?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The year covering <paramref name="date"/> for the company, or null when no year covers
    /// it (no year = open). A closed covering year is rejected by the caller via
    /// <see cref="FiscalYear.EnsurePostingAllowed"/>.
    /// </summary>
    Task<FiscalYear?> GetCoveringYearAsync(Guid companyId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>True when another year of the same company overlaps the window (spec FC-04).</summary>
    Task<bool> HasOverlapAsync(Guid companyId, DateOnly startDate, DateOnly endDate, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalYear year, CancellationToken cancellationToken = default);

    void Update(FiscalYear year);

    Task<List<FiscalYear>> GetPagedAsync(Guid companyId, bool? isClosed, int skip, int take, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="ExecuteInTransactionAsync{T}"/> but under
    /// <see cref="IsolationLevel.Serializable"/>: create/close/submit re-read the year, the
    /// overlap guard and the duplicate-close guard inside the txn (plan.md §6, spec FC-10).
    /// </summary>
    Task<T> ExecuteInSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plan.md §3 hard-lock rule, repository half: resolves the year covering
    /// <paramref name="postingDate"/> and throws when it is closed. Null covering year = open.
    /// Call AFTER <c>Company.EnsurePostingDateUnlocked</c> at every posting entry point.
    /// </summary>
    /// <exception cref="Exceptions.FiscalYearClosedException">Covering year is closed.</exception>
    Task EnsurePostingDateInOpenYearAsync(Guid companyId, DateOnly postingDate, CancellationToken cancellationToken = default);
}
