using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for Company lookups needed by the posting engine (decision D3): the
/// company supplies <c>AllowNegativeStock</c> (Task 3.3) and the
/// <c>StockReceivedAccountCode</c> GL default (spec ST-01).
/// </summary>
public interface ICompanyRepository
{
    /// <summary>The company with the given id, or null when it does not exist in this tenant.</summary>
    Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task UpdateCompanyAsync(Company company, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plan.md §3 hard-lock rule, repository half (R-13 FC-04): resolves the fiscal year covering
    /// <paramref name="postingDate"/> and throws when it is closed (null covering year = open).
    /// Call AFTER <c>Company.EnsurePostingDateUnlocked</c> at every posting entry point. Lives here
    /// (rather than only on <c>IFiscalYearRepository</c>) so the twenty existing pipelines gain the
    /// guard with a single line and zero DI churn — the company owns its fiscal calendar.
    /// </summary>
    /// <exception cref="Exceptions.FiscalYearClosedException">Covering year is closed.</exception>
    Task EnsurePostingDateInOpenYearAsync(Guid companyId, DateOnly postingDate, CancellationToken cancellationToken = default);
}
