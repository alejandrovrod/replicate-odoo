using System.Data;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    /// <summary>
    /// Constitution III.4: SELECT MAX(VoucherNo) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT
    /// posting transaction, scoped to (TenantId, CompanyId, year). A rolled-back submission
    /// consumes NO number.
    /// </summary>
    public async Task<string> NextPaymentVoucherNumberAsync(
        Guid companyId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Payment numbering must run inside the posting transaction (Constitution III.4): "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"PAY-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT MAX(VoucherNo) FROM dbo.PaymentEntry WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND VoucherNo LIKE @Pattern;";
        command.Transaction = transaction.GetDbTransaction();

        AddParameter(command, "@TenantId", tenantId);
        AddParameter(command, "@CompanyId", companyId);
        AddParameter(command, "@Pattern", pattern);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var max = scalar as string;

        var nextSequence = 1;
        if (!string.IsNullOrEmpty(max))
        {
            var separator = max.LastIndexOf('-');
            if (separator < 0 || !int.TryParse(max[(separator + 1)..], out var currentSequence))
            {
                throw new InvalidOperationException(
                    $"Stored payment voucher number '{max}' does not follow the PAY-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Payment voucher sequence for year {year} is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"PAY-{year}-{nextSequence:D5}";
    }

    public async Task AddPaymentAsync(PaymentEntry payment, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentEntries.AddAsync(payment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PaymentEntry?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => _dbContext.PaymentEntries
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

    public async Task UpdatePaymentAsync(PaymentEntry payment, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(PaymentEntry), payment.Id, ex);
        }
    }

    public async Task<PagedResult<PaymentEntry>> GetPaymentsPagedAsync(
        Guid companyId,
        PagedRequest paging,
        PaymentDocumentStatus? status,
        PaymentType? paymentType,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PaymentEntries.Where(p => p.CompanyId == companyId);

        if (status.HasValue)
        {
            query = query.Where(p => p.DocumentStatus == status.Value);
        }

        if (paymentType.HasValue)
        {
            query = query.Where(p => p.PaymentType == paymentType.Value);
        }

        return await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .ToPagedResultAsync(paging, cancellationToken);
    }

    public async Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _dbContext.GLEntries.AddRange(glEntries);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task AddAccountAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        await _dbContext.BankAccounts.AddAsync(account, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAccountAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(BankAccount), account.Id, ex);
        }
    }

    public async Task<PagedResult<BankAccount>> GetAccountsPagedAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankAccounts
            .Where(a => a.CompanyId == companyId)
            .OrderBy(a => a.AccountName)
            .ThenBy(a => a.Id)
            .ToPagedResultAsync(paging, cancellationToken);

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

    public async Task<PagedResult<BankTransactionRule>> GetRulesByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => await _dbContext.BankTransactionRules
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Priority)
            .ToPagedResultAsync(paging, cancellationToken);

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

    public async Task<PagedResult<BankTransaction>> GetTransactionsAsync(
        Guid companyId,
        Guid? bankAccountId,
        BankTransactionStatus? status,
        PagedRequest paging,
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
            .ToPagedResultAsync(paging, cancellationToken);
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
