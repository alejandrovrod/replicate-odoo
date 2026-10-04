using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBankRepository"/>: bank account reads, FITID
/// de-duplication reads, and atomic batch + staging-row persistence.
/// </summary>
/// <remarks>
/// Staging isolation BY CONSTRUCTION (invariant BN-01): this repository exposes no
/// <c>GLEntry</c> write path. There is deliberately NO manual
/// <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries (Constitution II.3 - the global
/// query filter does it).
/// </remarks>
public sealed class BankRepository : IBankRepository
{
    private readonly AppDbContext _dbContext;

    public BankRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Join an already-open transaction instead of creating a nested one (same DbContext instance).
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<BankAccount?> GetAccountByIdAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankAccounts
            .FirstOrDefaultAsync(a => a.Id == bankAccountId, cancellationToken);

    public async Task<IReadOnlySet<string>> GetImportedTransactionIdsAsync(
        Guid bankAccountId,
        IReadOnlyCollection<string> transactionIds,
        CancellationToken cancellationToken = default)
    {
        var found = await _dbContext.BankTransactions
            .Where(t => t.BankAccountId == bankAccountId
                && t.TransactionId != null
                && transactionIds.Contains(t.TransactionId))
            .Select(t => t.TransactionId!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(found, StringComparer.Ordinal);
    }

    public async Task AddImportAsync(BankStatementImport import, CancellationToken cancellationToken = default)
    {
        await _dbContext.BankStatementImports.AddAsync(import, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTransactionsAsync(
        IReadOnlyList<BankTransaction> transactions,
        CancellationToken cancellationToken = default)
    {
        _dbContext.BankTransactions.AddRange(transactions);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
