using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IGLEntryRepository"/> (tasks.md 2.5): the two reads behind
/// the financial reports - the paginated general ledger with full-set totals and the per-account
/// aggregates of plan.md §4's canonical Trial Balance SQL.
/// </summary>
/// <remarks>
/// <para><b>No manual <c>.Where(e =&gt; e.TenantId == ...)</c></b> (Constitution II.3): every
/// statement runs through AppDbContext, whose global query filter scopes GLEntry and Account to
/// the current tenant - the <c>CompanyId</c> predicates below are business scoping only (a report
/// belongs to one company).</para>
/// <para><b>Read-only</b>: no SaveChanges, no tracking surprises - the append-only law
/// (Constitution III.2) leaves mutations to the posting repositories, so a report can never write.</para>
/// </remarks>
public sealed class GLEntryRepository : IGLEntryRepository
{
    private readonly AppDbContext _dbContext;

    public GLEntryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GeneralLedgerPage> GetGeneralLedgerPageAsync(
        Guid companyId,
        GeneralLedgerFilter filter,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(
            _dbContext.GLEntries.Where(g => g.CompanyId == companyId),
            filter);

        // Totals of the FULL filtered set, computed by the database in ONE aggregate statement.
        // They are intentionally a separate query from the page below: the pinned contract wants
        // totals that describe every matching row even when `take` shows only the first few, and
        // a GroupBy-less aggregate over the whole filtered set is exactly that. An empty set
        // produces no group at all, hence the null fallback to 0.0000.
        var totals = await filtered
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Debit = group.Sum(g => g.Debit),
                Credit = group.Sum(g => g.Credit),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // The page itself: chronological audit order (PostingDate ASC, Id ASC) with the posting
        // account loaded, because every row of the report shows code/name/RootType next to the
        // amounts (FK_GLEntry_Account makes the join total - a row can never lose its account).
        var items = await filtered
            .Include(g => g.Account)
            .OrderBy(g => g.PostingDate)
            .ThenBy(g => g.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new GeneralLedgerPage(
            items,
            totals?.Debit ?? 0m,
            totals?.Credit ?? 0m);
    }

    public async Task<IReadOnlyList<AccountBalanceRow>> GetAccountBalancesAsync(
        Guid companyId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var ledger = _dbContext.GLEntries.Where(g => g.CompanyId == companyId);

        if (from is { } lowerBound)
        {
            ledger = ledger.Where(g => g.PostingDate >= lowerBound);
        }

        if (to is { } upperBound)
        {
            ledger = ledger.Where(g => g.PostingDate <= upperBound);
        }

        // plan.md §4 verbatim in LINQ: SUM per account, JOIN Account for the display columns and
        // the RootType/Type the reports group by. Grouping by account id means an account without
        // movement never reaches the result, and reversal rows stay included (a cancelled voucher
        // nets to zero instead of disappearing from history).
        var rows = await
            (from g in ledger
             join a in _dbContext.Accounts on g.AccountId equals a.Id
             where a.CompanyId == companyId
             group new { g.Debit, g.Credit }
                 by new { g.AccountId, a.AccountCode, a.AccountName, a.RootType, a.Type }
                 into grouped
             select new
             {
                 grouped.Key.AccountId,
                 grouped.Key.AccountCode,
                 grouped.Key.AccountName,
                 grouped.Key.RootType,
                 grouped.Key.Type,
                 TotalDebit = grouped.Sum(x => x.Debit),
                 TotalCredit = grouped.Sum(x => x.Credit),
             })
            .ToListAsync(cancellationToken);

        var balances = new List<AccountBalanceRow>(rows.Count);
        foreach (var row in rows)
        {
            balances.Add(new AccountBalanceRow(
                row.AccountId,
                row.AccountCode,
                row.AccountName,
                row.RootType,
                row.Type,
                row.TotalDebit,
                row.TotalCredit));
        }

        return balances;
    }

    /// <summary>
    /// Narrows a ledger query with the AND-combined report filters: every populated filter adds
    /// its own predicate, unset filters leave the query untouched.
    /// </summary>
    private static IQueryable<GLEntry> ApplyFilter(IQueryable<GLEntry> source, GeneralLedgerFilter filter)
    {
        if (filter.AccountId is { } accountId)
        {
            source = source.Where(g => g.AccountId == accountId);
        }

        if (filter.VoucherId is { } voucherId)
        {
            source = source.Where(g => g.VoucherId == voucherId);
        }

        if (!string.IsNullOrWhiteSpace(filter.VoucherType))
        {
            var voucherType = filter.VoucherType;
            source = source.Where(g => g.VoucherType == voucherType);
        }

        if (filter.From is { } from)
        {
            source = source.Where(g => g.PostingDate >= from);
        }

        if (filter.To is { } to)
        {
            source = source.Where(g => g.PostingDate <= to);
        }

        return source;
    }
}
