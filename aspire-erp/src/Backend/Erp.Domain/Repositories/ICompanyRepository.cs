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
}
