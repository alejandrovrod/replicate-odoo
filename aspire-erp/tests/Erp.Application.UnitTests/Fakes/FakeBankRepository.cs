using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IBankRepository"/>: seeded bank accounts, the import transaction simply
/// executes its callback, and every persisted batch/staging row is captured so tests can assert
/// on them without a database. Block B extends it with the rule store, the payment-voucher
/// store and the reconciliation link store behind the same seam.
/// </summary>
/// <remarks>
/// <see cref="AddedGlEntries"/> is ALWAYS empty: neither the import nor the reconcile path owns
/// a GL write (invariant BN-01 staging isolation by construction; GLEntry is append-only), and
/// the fake exposes the capture so tests can prove zero <c>GLEntry</c> writes the same way the
/// stock fake does. Optimistic-concurrency races are simulated with the
/// <c>FailNext*Update</c> flags (the FakePurchaseRepository pattern): the next update throws
/// <see cref="ConcurrencyConflictException"/> like the real repository does after a RowVersion
/// mismatch, then the flag resets so only one call fails. Client-supplied stale tokens are
/// compared by the handlers themselves (compare-and-swap pre-check) against the seeded
/// <c>RowVersion</c> values, which are real byte arrays on the seeded entities.
/// </remarks>
public sealed class FakeBankRepository : IBankRepository
{
    private readonly List<BankAccount> _accounts = new();
    private readonly List<BankStatementImport> _imports = new();
    private readonly List<BankTransaction> _persistedTransactions = new();
    private readonly List<BankTransaction> _addedTransactions = new();
    private readonly List<GLEntry> _addedGl = new();
    private readonly List<BankTransactionRule> _rules = new();
    private readonly List<PaymentEntry> _entries = new();
    private readonly List<BankReconciliation> _links = new();

    /// <summary>Batches persisted so far.</summary>
    public IReadOnlyList<BankStatementImport> PersistedImports => _imports;

    /// <summary>All staging rows persisted so far (seeded + imported).</summary>
    public IReadOnlyList<BankTransaction> PersistedTransactions => _persistedTransactions;

    /// <summary>Staging rows written by the import under test.</summary>
    public IReadOnlyList<BankTransaction> AddedTransactions => _addedTransactions;

    /// <summary>Always empty: neither path writes GL rows (BN-01, III.2).</summary>
    public IReadOnlyList<GLEntry> AddedGlEntries => _addedGl;

    /// <summary>All heuristic rules (seeded + created).</summary>
    public IReadOnlyList<BankTransactionRule> Rules => _rules;

    /// <summary>All payment vouchers (seeded).</summary>
    public IReadOnlyList<PaymentEntry> PaymentEntries => _entries;

    /// <summary>All reconciliation links (reconcile adds, un-reconcile removes).</summary>
    public IReadOnlyList<BankReconciliation> Links => _links;

    /// <summary>Number of transactions opened (proves each workflow runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    /// <summary>
    /// When set, the NEXT <c>UpdateTransactionAsync</c> fails like the real repository does after
    /// a RowVersion mismatch; the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextTransactionUpdate { get; set; }

    /// <summary>
    /// When set, the NEXT <c>UpdatePaymentEntryAsync</c> fails like the real repository does
    /// after a RowVersion mismatch; the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextPaymentEntryUpdate { get; set; }

    public void SeedAccount(BankAccount account) => _accounts.Add(account);

    public void SeedTransaction(BankTransaction transaction) => _persistedTransactions.Add(transaction);

    public void SeedRule(BankTransactionRule rule) => _rules.Add(rule);

    public void SeedPaymentEntry(PaymentEntry entry) => _entries.Add(entry);

    public void SeedLink(BankReconciliation link) => _links.Add(link);

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task<BankAccount?> GetAccountByIdAsync(Guid bankAccountId, CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.FirstOrDefault(a => a.Id == bankAccountId));

    /// <summary>Payment vouchers persisted by the R-12 handlers under test.</summary>
    public IReadOnlyList<PaymentEntry> AddedPayments => _addedPayments;

    private readonly List<PaymentEntry> _addedPayments = new();
    private int _paymentSequence;

    public Task<string> NextPaymentVoucherNumberAsync(
        Guid companyId, int year, CancellationToken cancellationToken = default)
        => Task.FromResult($"PAY-{year}-{++_paymentSequence:D5}");

    public Task AddPaymentAsync(PaymentEntry payment, CancellationToken cancellationToken = default)
    {
        _addedPayments.Add(payment);
        _entries.Add(payment);
        return Task.CompletedTask;
    }

    public Task<PaymentEntry?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_entries.FirstOrDefault(p => p.Id == paymentId));

    public Task UpdatePaymentAsync(PaymentEntry payment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<PagedResult<PaymentEntry>> GetPaymentsPagedAsync(
        Guid companyId,
        PagedRequest paging,
        PaymentDocumentStatus? status,
        PaymentType? paymentType,
        CancellationToken cancellationToken = default)
    {
        var items = _entries
            .Where(p => p.CompanyId == companyId
                && (!status.HasValue || p.DocumentStatus == status.Value)
                && (!paymentType.HasValue || p.PaymentType == paymentType.Value))
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .ToList();
        return Task.FromResult(new PagedResult<PaymentEntry>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _addedGl.AddRange(glEntries);
        return Task.CompletedTask;
    }

    public BankAccount? AddedAccount { get; private set; }

    public Task AddAccountAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        AddedAccount = account;
        _accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task UpdateAccountAsync(BankAccount account, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<PagedResult<BankAccount>> GetAccountsPagedAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var items = _accounts.Where(a => a.CompanyId == companyId).OrderBy(a => a.AccountName).ThenBy(a => a.Id).ToList();
        return Task.FromResult(new PagedResult<BankAccount>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<IReadOnlyList<BankAccount>> GetAccountsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BankAccount>>(
            _accounts.Where(a => a.CompanyId == companyId).ToList());

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

    public Task<IReadOnlyList<BankTransactionRule>> GetActiveRulesAsync(
        Guid companyId,
        Guid? bankAccountId,
        CancellationToken cancellationToken = default)
    {
        var query = _rules.Where(r => r.CompanyId == companyId && r.IsActive);

        if (bankAccountId is not null)
        {
            query = query.Where(r => r.BankAccountId == null || r.BankAccountId == bankAccountId);
        }

        // Same precedence as the EF implementation: account-scoped first, then global,
        // priority ascending inside each group.
        return Task.FromResult<IReadOnlyList<BankTransactionRule>>(query
            .OrderByDescending(r => r.BankAccountId != null)
            .ThenBy(r => r.Priority)
            .ToList());
    }

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository).
    public Task<PagedResult<BankTransactionRule>> GetRulesByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var items = _rules
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Priority)
            .ToList();
        return Task.FromResult(new PagedResult<BankTransactionRule>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task AddRuleAsync(BankTransactionRule rule, CancellationToken cancellationToken = default)
    {
        _rules.Add(rule);
        return Task.CompletedTask;
    }

    public Task<BankTransaction?> GetTransactionByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_persistedTransactions.FirstOrDefault(t => t.Id == transactionId));

    public Task<IReadOnlyList<BankTransaction>> GetUnreconciledTransactionsAsync(
        Guid companyId,
        Guid? bankAccountId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BankTransaction>>(_persistedTransactions
            .Where(t => t.CompanyId == companyId && t.Status == BankTransactionStatus.Unreconciled)
            .Where(t => bankAccountId == null || t.BankAccountId == bankAccountId)
            .ToList());

    public Task<PagedResult<BankTransaction>> GetTransactionsAsync(
        Guid companyId,
        Guid? bankAccountId,
        BankTransactionStatus? status,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var items = _persistedTransactions
            .Where(t => t.CompanyId == companyId)
            .Where(t => bankAccountId == null || t.BankAccountId == bankAccountId)
            .Where(t => status == null || t.Status == status)
            .OrderByDescending(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToList();
        return Task.FromResult(new PagedResult<BankTransaction>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task UpdateTransactionAsync(BankTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (FailNextTransactionUpdate)
        {
            FailNextTransactionUpdate = false;
            throw new ConcurrencyConflictException(nameof(BankTransaction), transaction.Id);
        }

        // In-memory: the entity instance IS the store; the mutation is already applied.
        return Task.CompletedTask;
    }

    public Task<PaymentEntry?> GetPaymentEntryByIdAsync(
        Guid paymentEntryId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_entries.FirstOrDefault(p => p.Id == paymentEntryId));

    public Task UpdatePaymentEntryAsync(PaymentEntry entry, CancellationToken cancellationToken = default)
    {
        if (FailNextPaymentEntryUpdate)
        {
            FailNextPaymentEntryUpdate = false;
            throw new ConcurrencyConflictException(nameof(PaymentEntry), entry.Id);
        }

        // In-memory: the entity instance IS the store; the mutation is already applied.
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BankReconciliation>> GetReconciliationsByTransactionAsync(
        Guid bankTransactionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BankReconciliation>>(_links
            .Where(l => l.BankTransactionId == bankTransactionId)
            .ToList());

    public Task<decimal> GetConsumedAmountForPaymentEntryAsync(
        Guid paymentEntryId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_links
            .Where(l => l.CounterpartType == BankReconciliationCounterpartType.PaymentEntry
                && l.CounterpartId == paymentEntryId)
            .Sum(l => l.AllocatedAmount));

    public Task AddReconciliationsAsync(
        IReadOnlyList<BankReconciliation> links,
        CancellationToken cancellationToken = default)
    {
        _links.AddRange(links);
        return Task.CompletedTask;
    }

    public Task RemoveReconciliationsAsync(
        Guid bankTransactionId,
        CancellationToken cancellationToken = default)
    {
        _links.RemoveAll(l => l.BankTransactionId == bankTransactionId);
        return Task.CompletedTask;
    }
}
