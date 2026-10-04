using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IBankRepository"/>: seeded bank accounts, the import transaction simply
/// executes its callback, and every persisted batch/staging row is captured so tests can assert
/// on them without a database.
/// </summary>
/// <remarks>
/// <see cref="AddedGlEntries"/> is ALWAYS empty: the import handler has no GL dependency
/// (invariant BN-01 staging isolation by construction), and the fake exposes the capture so
/// tests can prove zero <c>GLEntry</c> writes the same way the stock fake does.
/// </remarks>
public sealed class FakeBankRepository : IBankRepository
{
    private readonly List<BankAccount> _accounts = new();
    private readonly List<BankStatementImport> _imports = new();
    private readonly List<BankTransaction> _persistedTransactions = new();
    private readonly List<BankTransaction> _addedTransactions = new();
    private readonly List<GLEntry> _addedGl = new();

    /// <summary>Batches persisted so far.</summary>
    public IReadOnlyList<BankStatementImport> PersistedImports => _imports;

    /// <summary>All staging rows persisted so far (seeded + imported).</summary>
    public IReadOnlyList<BankTransaction> PersistedTransactions => _persistedTransactions;

    /// <summary>Staging rows written by the import under test.</summary>
    public IReadOnlyList<BankTransaction> AddedTransactions => _addedTransactions;

    /// <summary>Always empty: the import path writes no GL rows (BN-01).</summary>
    public IReadOnlyList<GLEntry> AddedGlEntries => _addedGl;

    /// <summary>Number of transactions opened (proves the import runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    public void SeedAccount(BankAccount account) => _accounts.Add(account);

    public void SeedTransaction(BankTransaction transaction) => _persistedTransactions.Add(transaction);

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task<BankAccount?> GetAccountByIdAsync(Guid bankAccountId, CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.FirstOrDefault(a => a.Id == bankAccountId));

    public Task<IReadOnlySet<string>> GetImportedTransactionIdsAsync(
        Guid bankAccountId,
        IReadOnlyCollection<string> transactionIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<string>>(
            _persistedTransactions
                .Where(t => t.BankAccountId == bankAccountId
                    && t.TransactionId != null
                    && transactionIds.Contains(t.TransactionId))
                .Select(t => t.TransactionId!)
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal));

    public Task AddImportAsync(BankStatementImport import, CancellationToken cancellationToken = default)
    {
        _imports.Add(import);
        return Task.CompletedTask;
    }

    public Task AddTransactionsAsync(IReadOnlyList<BankTransaction> transactions, CancellationToken cancellationToken = default)
    {
        _addedTransactions.AddRange(transactions);
        _persistedTransactions.AddRange(transactions);
        return Task.CompletedTask;
    }
}
