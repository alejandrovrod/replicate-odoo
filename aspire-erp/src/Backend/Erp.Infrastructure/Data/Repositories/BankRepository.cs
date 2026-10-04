using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBankRepository"/>: bank account reads, FITID
/// de-duplication reads, atomic batch + staging-row persistence, the heuristic rule store
/// (task 6.3) and the reconciliation link store (task 6.4).
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

    public async Task<IReadOnlyList<BankAccount>> GetAccountsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankAccounts
            .Where(a => a.CompanyId == companyId)
            .ToListAsync(cancellationToken);

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

    public async Task<IReadOnlyList<BankTransactionRule>> GetActiveRulesAsync(
        Guid companyId,
        Guid? bankAccountId,
        CancellationToken cancellationToken = default)
    {
        // Null account = company-wide run: return every active rule; the handler enforces
        // per-transaction scoping (a scoped rule only fires on its own account's lines).
        var query = _dbContext.BankTransactionRules
            .Where(r => r.CompanyId == companyId && r.IsActive);

        if (bankAccountId is not null)
        {
            query = query.Where(r => r.BankAccountId == null || r.BankAccountId == bankAccountId);
        }

        var rules = await query
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

        // Account-scoped rules first, then global ones, keeping priority order inside each
        // group (documented precedence for the handler's first-win loop).
        return rules
            .OrderByDescending(r => r.BankAccountId != null)
            .ThenBy(r => r.Priority)
            .ToList();
    }

    public async Task<IReadOnlyList<BankTransactionRule>> GetRulesByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankTransactionRules
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

    public async Task AddRuleAsync(BankTransactionRule rule, CancellationToken cancellationToken = default)
    {
        await _dbContext.BankTransactionRules.AddAsync(rule, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<BankTransaction?> GetTransactionByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

    public async Task<IReadOnlyList<BankTransaction>> GetUnreconciledTransactionsAsync(
        Guid companyId,
        Guid? bankAccountId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.BankTransactions
            .Where(t => t.CompanyId == companyId && t.Status == BankTransactionStatus.Unreconciled);

        if (bankAccountId is not null)
        {
            query = query.Where(t => t.BankAccountId == bankAccountId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BankTransaction>> GetTransactionsAsync(
        Guid companyId,
        Guid? bankAccountId,
        BankTransactionStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.BankTransactions.Where(t => t.CompanyId == companyId);

        if (bankAccountId is not null)
        {
            query = query.Where(t => t.BankAccountId == bankAccountId);
        }

        if (status is not null)
        {
            query = query.Where(t => t.Status == status);
        }

        return await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateTransactionAsync(BankTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Scenario BN-07: another clerk matched/reconciled this line between our load and
            // our save - the RowVersion WHERE clause matched 0 rows.
            throw new ConcurrencyConflictException(nameof(BankTransaction), transaction.Id, ex);
        }
    }

    public async Task<PaymentEntry?> GetPaymentEntryByIdAsync(
        Guid paymentEntryId,
        CancellationToken cancellationToken = default)
        => await _dbContext.PaymentEntries
            .FirstOrDefaultAsync(p => p.Id == paymentEntryId, cancellationToken);

    public async Task UpdatePaymentEntryAsync(PaymentEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(PaymentEntry), entry.Id, ex);
        }
    }

    public async Task<IReadOnlyList<BankReconciliation>> GetReconciliationsByTransactionAsync(
        Guid bankTransactionId,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankReconciliations
            .Where(l => l.BankTransactionId == bankTransactionId)
            .ToListAsync(cancellationToken);

    public async Task<decimal> GetConsumedAmountForPaymentEntryAsync(
        Guid paymentEntryId,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankReconciliations
            .Where(l => l.CounterpartType == BankReconciliationCounterpartType.PaymentEntry
                && l.CounterpartId == paymentEntryId)
            .SumAsync(l => (decimal?)l.AllocatedAmount, cancellationToken) ?? 0m;

    public async Task AddReconciliationsAsync(
        IReadOnlyList<BankReconciliation> links,
        CancellationToken cancellationToken = default)
    {
        _dbContext.BankReconciliations.AddRange(links);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveReconciliationsAsync(
        Guid bankTransactionId,
        CancellationToken cancellationToken = default)
    {
        var links = await _dbContext.BankReconciliations
            .Where(l => l.BankTransactionId == bankTransactionId)
            .ToListAsync(cancellationToken);

        _dbContext.BankReconciliations.RemoveRange(links);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
