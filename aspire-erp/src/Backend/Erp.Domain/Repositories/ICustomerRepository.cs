using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the Customer master (Task 5.1). Implemented by
/// Erp.Infrastructure.Data.Repositories.CustomerRepository.
/// </summary>
/// <remarks>
/// Tenant isolation is AUTOMATIC (Constitution II.3): implementations query through AppDbContext,
/// whose global filter scopes every read to the current tenant. Unlike Suppliers (tenant-wide),
/// a Customer belongs to ONE company (plan.md §1 CompanyId NOT NULL), so every read here is
/// additionally company-scoped - the same split as <see cref="IAccountRepository"/>.
/// </remarks>
public interface ICustomerRepository
{
    /// <summary>Persists a new customer (TenantId is stamped by AppDbContext, never passed here).</summary>
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    /// <summary>True when the code already exists in this company (plan.md §1 UQ_Customer_Tenant_Company_Code).</summary>
    Task<bool> ExistsCodeAsync(Guid companyId, string customerCode, CancellationToken cancellationToken = default);

    /// <summary>The customer with the given id, or null when it does not exist in this tenant.</summary>
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>The most recent customers of the company (newest first) for the list view.</summary>
    Task<PagedResult<Customer>> GetRecentAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>Persists an updated customer.</summary>
    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}
