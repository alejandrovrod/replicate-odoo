using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the bank statement import &amp; staging engine (task 6.2).
/// Implemented by Erp.Infrastructure.Data.Repositories.BankRepository.
/// </summary>
/// <remarks>
/// Staging isolation BY CONTRACT (invariant BN-01): this interface exposes NO General Ledger
/// write path - the import handler physically cannot post a <c>GLEntry</c>. All writes run
/// through <see cref="ExecuteInTransactionAsync{T}"/> so the batch header and its staging rows
/// commit atomically (one transaction, rollback on ANY failure - zero partial imports).
/// Tenant isolation stays automatic (Constitution II.3).
/// </remarks>
public interface IBankRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole import back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Gets a bank account by its ID (the handler rejects unknown or foreign accounts).</summary>
    Task<BankAccount?> GetAccountByIdAsync(Guid bankAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subset of <paramref name="transactionIds"/> (bank FITIDs) already imported for the
    /// account - the de-duplication read for idempotent re-imports (scenario BN-05).
    /// </summary>
    Task<IReadOnlySet<string>> GetImportedTransactionIdsAsync(
        Guid bankAccountId,
        IReadOnlyCollection<string> transactionIds,
        CancellationToken cancellationToken = default);

    /// <summary>Persists one import batch header (inside the ambient import transaction).</summary>
    Task AddImportAsync(BankStatementImport import, CancellationToken cancellationToken = default);

    /// <summary>Persists the staging rows of a batch (inside the ambient import transaction).</summary>
    Task AddTransactionsAsync(IReadOnlyList<BankTransaction> transactions, CancellationToken cancellationToken = default);
}
