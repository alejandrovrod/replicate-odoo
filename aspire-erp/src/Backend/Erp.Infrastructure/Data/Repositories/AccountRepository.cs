using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAccountRepository"/> (decision C3). Every query goes
/// through AppDbContext, whose global query filter isolates rows by tenant - there is deliberately
/// NO manual <c>.Where(a =&gt; a.TenantId == ...)</c> here (Constitution Article II.3).
/// The CompanyId predicates below are business scoping (a company owns its COA), not tenancy.
/// </summary>
public sealed class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _dbContext;

    public AccountRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await _dbContext.Accounts.AddAsync(account, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetByIdWithAncestorsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var chain = new List<Account>();
        var visited = new HashSet<Guid>();
        Guid? nextId = accountId;

        while (nextId is { } id)
        {
            if (visited.Contains(id))
            {
                // The stored parent chain loops back on itself (data corruption). Append the
                // repeated node so AccountValidator.EnsureNoCycle sees the duplicate and fails the
                // request instead of walking forever.
                var repeated = chain.First(a => a.Id == id);
                chain.Add(repeated);
                break;
            }

            visited.Add(id);

            var current = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (current is null)
            {
                break;
            }

            chain.Add(current);
            nextId = current.ParentAccountId;
        }

        return chain;
    }

    public Task<bool> ExistsByCodeAsync(Guid companyId, string accountCode, CancellationToken cancellationToken = default)
        => _dbContext.Accounts.AnyAsync(
            a => a.CompanyId == companyId && a.AccountCode == accountCode,
            cancellationToken);

    public async Task<IReadOnlyList<Account>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.Accounts
            .Where(a => a.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    public async Task<IReadOnlyList<Account>> FindActiveLeafByCodeAsync(
        Guid companyId,
        string accountCode,
        CancellationToken cancellationToken = default)
        => await _dbContext.Accounts
            .Where(a => a.CompanyId == companyId
                && a.AccountCode == accountCode
                && a.IsActive
                && !a.IsGroup)
            .ToListAsync(cancellationToken);
}
