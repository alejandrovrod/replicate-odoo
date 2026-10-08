using Erp.Domain.Common;
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
    /// Next gapless payment voucher number, e.g. 2026 -> "PAY-2026-00001" (Constitution III.4:
    /// SELECT MAX(VoucherNo) WITH (UPDLOCK, HOLDLOCK) over dbo.PaymentEntry inside the AMBIENT
    /// posting transaction; a rollback consumes no number).
    /// </summary>
    Task<string> NextPaymentVoucherNumberAsync(
        Guid companyId, int year, CancellationToken cancellationToken = default);

    /// <summary>Persists a Draft payment voucher with its allocation slices.</summary>
    Task AddPaymentAsync(PaymentEntry payment, CancellationToken cancellationToken = default);

    /// <summary>The payment voucher with its allocations, or null.</summary>
    Task<PaymentEntry?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves status/amount transitions of an already-tracked payment. Translates EF's
    /// <c>DbUpdateConcurrencyException</c> into <see cref="Exceptions.ConcurrencyConflictException"/>.
    /// </summary>
    Task UpdatePaymentAsync(PaymentEntry payment, CancellationToken cancellationToken = default);

    /// <summary>Company payment vouchers, newest first (optional status/type filters).</summary>
    Task<PagedResult<PaymentEntry>> GetPaymentsPagedAsync(
        Guid companyId,
        PagedRequest paging,
        PaymentDocumentStatus? status,
        PaymentType? paymentType,
        CancellationToken cancellationToken = default);

    /// <summary>Persists General Ledger lines of a payment posting (inside the ambient transaction).</summary>
    Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default);

    /// <summary>Persists a new bank account master row.</summary>
    Task AddAccountAsync(BankAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves mutations of an already-tracked bank account. Translates EF's
    /// <c>DbUpdateConcurrencyException</c> (RowVersion WHERE clause matched 0 rows) into
    /// <see cref="Exceptions.ConcurrencyConflictException"/>.
    /// </summary>
    Task UpdateAccountAsync(BankAccount account, CancellationToken cancellationToken = default);

    /// <summary>Bank accounts of a company, ordered by name (master-data list read).</summary>
    Task<PagedResult<BankAccount>> GetAccountsPagedAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>All bank accounts of a company (tenant source for global rules).</summary>
    Task<IReadOnlyList<BankAccount>> GetAccountsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Active rules for a company ordered by <c>Priority</c> ascending, account-scoped rules for
    /// <paramref name="bankAccountId"/> first, then global rules (null account) - the handler
    /// documents this precedence and the first matching rule wins per transaction.
    /// </summary>
    Task<IReadOnlyList<BankTransactionRule>> GetActiveRulesAsync(
        Guid companyId,
        Guid? bankAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>All rules of a company (active and inactive) for the management read.</summary>
    Task<PagedResult<BankTransactionRule>> GetRulesByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>Persists one heuristic rule (inside the ambient transaction).</summary>
    Task AddRuleAsync(BankTransactionRule rule, CancellationToken cancellationToken = default);

    /// <summary>Gets one staging transaction by id (reconciliation + rule-run targeting).</summary>
    Task<BankTransaction?> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unreconciled staging transactions of a company (optionally one account) - the rule-run
    /// candidate set. <c>Matched</c> lines are NOT re-processed: a rule match is terminal until
    /// a reconcile / un-reconcile moves the line again.
    /// </summary>
    Task<IReadOnlyList<BankTransaction>> GetUnreconciledTransactionsAsync(
        Guid companyId,
        Guid? bankAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets staging transactions of a company (optionally filtered) for the list read.</summary>
    Task<PagedResult<BankTransaction>> GetTransactionsAsync(
        Guid companyId,
        Guid? bankAccountId,
        BankTransactionStatus? status,
        PagedRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the mutated staging transaction. Translates EF's
    /// <c>DbUpdateConcurrencyException</c> (RowVersion WHERE clause matched 0 rows) into
    /// <see cref="Exceptions.ConcurrencyConflictException"/>, mirroring
    /// PurchaseRepository.UpdateOrderAsync.
    /// </summary>
    Task UpdateTransactionAsync(BankTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Gets a payment voucher by id (reconciliation counterpart).</summary>
    Task<PaymentEntry?> GetPaymentEntryByIdAsync(Guid paymentEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the mutated payment voucher. Same concurrency translation as
    /// <see cref="UpdateTransactionAsync"/>.
    /// </summary>
    Task UpdatePaymentEntryAsync(PaymentEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Allocation slices already recorded for one staging transaction.</summary>
    Task<IReadOnlyList<BankReconciliation>> GetReconciliationsByTransactionAsync(
        Guid bankTransactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Total amount already consumed from one payment voucher across ALL reconciliations
    /// (the over-consumption guard reads this).
    /// </summary>
    Task<decimal> GetConsumedAmountForPaymentEntryAsync(
        Guid paymentEntryId,
        CancellationToken cancellationToken = default);

    /// <summary>Persists reconciliation links (inside the ambient reconcile transaction).</summary>
    Task AddReconciliationsAsync(IReadOnlyList<BankReconciliation> links, CancellationToken cancellationToken = default);

    /// <summary>Deletes the links of one staging transaction (un-reconcile path).</summary>
    Task RemoveReconciliationsAsync(Guid bankTransactionId, CancellationToken cancellationToken = default);
}
