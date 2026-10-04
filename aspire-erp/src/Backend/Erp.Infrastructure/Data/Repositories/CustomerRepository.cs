using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICustomerRepository"/> (Task 5.1).
/// </summary>
/// <remarks>
/// There is deliberately NO manual <c>.Where(c =&gt; c.TenantId == ...)</c> on LINQ queries
/// (Constitution II.3 - the global query filter does it). The CompanyId predicates are business
/// scoping (a customer belongs to one company, plan.md §1), not tenancy - the same split as
/// <see cref="AccountRepository"/>.
/// </remarks>
public sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _dbContext;

    public CustomerRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // UQ_Customer_Tenant_Company_Code (plan.md §1) is the hard backstop of the
            // duplicate-code rule: the handler's ExistsCodeAsync pre-check can be raced by a
            // concurrent insert, so translate the race into the same domain failure the pre-check
            // raises instead of leaking an EF/SQL exception to the API layer (mirrors
            // AccountRepository.AddAsync).
            _dbContext.Entry(customer).State = EntityState.Detached;
            throw new CustomerValidationException(
                SellingErrorCodes.DuplicateCustomerCode,
                $"Customer code '{customer.CustomerCode}' already exists in this company.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    public Task<bool> ExistsCodeAsync(Guid companyId, string customerCode, CancellationToken cancellationToken = default)
        => _dbContext.Customers.AnyAsync(
            c => c.CompanyId == companyId && c.CustomerCode == customerCode,
            cancellationToken);

    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _dbContext.Customers.Update(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> GetRecentAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.Customers
            .Where(c => c.CompanyId == companyId)

            // plan.md §1 defines no CreatedAt column, and Id is NEWSEQUENTIALID() (monotonic by
            // insert order), so id descending is the "newest first" ordering of the list view -
            // the same expression SupplierRepository uses via CreatedAt.
            .OrderByDescending(c => c.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
