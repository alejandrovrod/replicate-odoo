using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="IAccountRepository"/> for the posting-engine lookups (III.3).</summary>
public sealed class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new();

    /// <summary>Chain returned by GetByIdWithAncestorsAsync: [self, parent, grandparent, ...].</summary>
    public IReadOnlyList<Account> Ancestors { get; set; } = Array.Empty<Account>();

    public IReadOnlyList<Account> CompanyAccounts { get; set; } = Array.Empty<Account>();

    public bool CodeExists { get; set; }

    public Account? AddedAccount { get; private set; }

    /// <summary>Answer returned by FindActiveLeafByCodeAsync for company-level GL defaults.</summary>
    public IReadOnlyList<Account> AccountsByCode { get; set; } = Array.Empty<Account>();

    /// <summary>
    /// Per-code answers for FindActiveLeafByCodeAsync (the buying engine resolves several company
    /// codes - 2120 / 2110 / 1130 / 5120 - in the same posting). Keys not present fall back to
    /// <see cref="AccountsByCode"/>, which keeps the stock tests working unchanged.
    /// </summary>
    public Dictionary<string, IReadOnlyList<Account>> AccountsByCodeMap { get; } = new();

    public void Seed(params Account[] accounts) => _accounts.AddRange(accounts);

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        AddedAccount = account;
        _accounts.Add(account);
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

    /// <summary>Resolves by id from the seeded accounts (falling back to the explicit override).</summary>
    public Account? AccountById { get; set; }

    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.FirstOrDefault(a => a.Id == accountId) ?? AccountById);

    public Task<IReadOnlyList<Account>> FindActiveLeafByCodeAsync(
        Guid companyId,
        string accountCode,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            AccountsByCodeMap.TryGetValue(accountCode, out var mapped) ? mapped : AccountsByCode);
}

