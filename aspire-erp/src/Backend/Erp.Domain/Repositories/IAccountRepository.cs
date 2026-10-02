using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the Account aggregate (plan.md 1.2 puts repository interfaces in
/// Erp.Domain/Repositories so Erp.Application never references EF Core - Constitution I.3).
/// Implemented by Erp.Infrastructure.Data.Repositories.AccountRepository.
/// </summary>
/// <remarks>
/// Tenant isolation is AUTOMATIC: implementations query through AppDbContext, whose global query
/// filter scopes every read to the current tenant (Constitution II.3 - manual
/// <c>.Where(e =&gt; e.TenantId == ...)</c> in these implementations is forbidden).
/// </remarks>
public interface IAccountRepository
{
    /// <summary>Persists a new account (TenantId is stamped by AppDbContext, never passed here).</summary>
    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the requested account followed by its ancestors - parent, grandparent, ..., root
    /// (index 0 = the account itself). Empty list when the account does not exist (or belongs to
    /// another tenant, which the global query filter treats as non-existent).
    /// </summary>
    Task<IReadOnlyList<Account>> GetByIdWithAncestorsAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>True when the code already exists within the given company (codes are unique per company, not per tenant).</summary>
    Task<bool> ExistsByCodeAsync(Guid companyId, string accountCode, CancellationToken cancellationToken = default);

    /// <summary>All accounts of one company - the flat source the tree query assembles into a hierarchy.</summary>
    Task<IReadOnlyList<Account>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}
