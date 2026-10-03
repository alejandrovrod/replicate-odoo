using System.Data;
using System.Data.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IJournalRepository"/>: draft persistence, workflow
/// transitions, the JV gapless voucher generator (Constitution III.4) and the GLEntry appends of
/// submit/cancel.
/// </summary>
/// <remarks>
/// Every write runs inside <see cref="ExecuteInTransactionAsync{T}"/> so a posting is atomic
/// (tasks.md 2.4 acceptance: "updates ledger balances atomically"). There is deliberately NO
/// manual <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries (Constitution II.3 - the global
/// query filter does it); the raw SQL voucher statement scopes by TenantId itself because EF query
/// filters do NOT apply to raw SQL. The numbering algorithm is the same one StockRepository and
/// PurchaseRepository implement (per-repository private copy is the established pattern; see the
/// report note about extracting a shared helper).
/// </remarks>
public sealed class JournalRepository : IJournalRepository
{
    /// <summary>Table that owns the JV sequence (interpolated as a literal - SQL identifiers cannot be parameterized).</summary>
    private const string JournalTable = "dbo.JournalEntry";

    private readonly AppDbContext _dbContext;

    public JournalRepository(AppDbContext dbContext)
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

    // ------------------------------------------------------------------- voucher numbering (III.4)

    public async Task<string> NextVoucherNumberAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Voucher numbering must run inside the posting transaction (Constitution III.4): "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        // EF's global query filter does NOT apply to raw SQL, so the tenant scope is written out
        // explicitly here (it reproduces exactly what the filter would have done).
        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"{prefix}-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT MAX(VoucherNo) FROM {JournalTable} WITH (UPDLOCK, HOLDLOCK) "
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
            // Fixed 5-digit zero padding keeps lexicographic and numeric order identical.
            var separator = max.LastIndexOf('-');
            if (separator < 0 || !int.TryParse(max[(separator + 1)..], out var currentSequence))
            {
                throw new InvalidOperationException(
                    $"Stored voucher number '{max}' does not follow the PREFIX-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Voucher sequence for '{prefix}-{year}' is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"{prefix}-{year}-{nextSequence:D5}";
    }

    // ------------------------------------------------------------------------------- persistence

    public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        await _dbContext.JournalEntries.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another request transitioned this voucher (or touched it) between our load and our
            // save - the RowVersion WHERE clause matched 0 rows. Translate EF's exception into the
            // typed domain failure the handlers convert into a 409 Result.Failure, mirroring
            // PurchaseRepository.UpdateOrderAsync.
            throw new ConcurrencyConflictException(nameof(JournalEntry), entry.Id, ex);
        }
    }

    public async Task AddGlEntriesAsync(
        IReadOnlyList<GLEntry> glEntries,
        CancellationToken cancellationToken = default)
    {
        _dbContext.GLEntries.AddRange(glEntries);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------------------------- reads

    public async Task<JournalEntry?> GetByIdAsync(
        Guid journalEntryId,
        CancellationToken cancellationToken = default)
        => await _dbContext.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.Id == journalEntryId, cancellationToken);

    public async Task<IReadOnlyList<JournalEntry>> GetRecentByCompanyAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.JournalEntries
            .Where(j => j.CompanyId == companyId)
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
