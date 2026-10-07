using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Domain.UnitTests;

/// <summary>
/// In-memory <see cref="IAccountRepository"/> double: no EF, no database. Tests configure the
/// ancestor chain, the company's accounts and the duplicate-code answer per test.
/// </summary>
public sealed class FakeAccountRepository : IAccountRepository
{
    /// <summary>Chain returned by GetByIdWithAncestorsAsync: [self, parent, grandparent, ...].</summary>
    public IReadOnlyList<Account> Ancestors { get; set; } = Array.Empty<Account>();

    /// <summary>Accounts returned by GetByCompanyAsync.</summary>
    public IReadOnlyList<Account> CompanyAccounts { get; set; } = Array.Empty<Account>();

    public bool CodeExists { get; set; }

    public Account? AddedAccount { get; private set; }

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        AddedAccount = account;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Account>> GetByIdWithAncestorsAsync(Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult(Ancestors);

    public Task<bool> ExistsByCodeAsync(Guid companyId, string accountCode, CancellationToken cancellationToken = default)
        => Task.FromResult(CodeExists);

    public Task<IReadOnlyList<Account>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult(CompanyAccounts);

    /// <summary>Account returned by GetByIdAsync (needed by Constitution III.3 lookups).</summary>
    public Account? AccountById { get; set; }

    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult(AccountById);

    /// <summary>Answer returned by FindActiveLeafByCodeAsync for company-level GL defaults.</summary>
    public IReadOnlyList<Account> AccountsByCode { get; set; } = Array.Empty<Account>();

    public Task<IReadOnlyList<Account>> FindActiveLeafByCodeAsync(
        Guid companyId,
        string accountCode,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AccountsByCode);
}

