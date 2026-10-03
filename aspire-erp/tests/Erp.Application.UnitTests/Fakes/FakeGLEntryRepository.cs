using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IGLEntryRepository"/>: keeps the seeded ledger rows and reproduces the two
/// reads of tasks.md 2.5 exactly as the EF implementation promises them - the page ordered by
/// PostingDate ASC / Id ASC with totals taken over the FULL filtered set (never over the page),
/// and the per-account aggregates that only include accounts with movement.
/// </summary>
/// <remarks>
/// Rows must carry their <see cref="GLEntry.Account"/> navigation, because that is how the real
/// query joins Account for the display columns; a row without one is a broken seed and fails
/// loudly instead of silently producing a report with missing accounts.
/// </remarks>
public sealed class FakeGLEntryRepository : IGLEntryRepository
{
    private readonly List<GLEntry> _rows = new();

    /// <summary>Seeds ledger rows (their <c>Account</c> navigation must be populated).</summary>
    public void Seed(params GLEntry[] rows) => _rows.AddRange(rows);

    public Task<GeneralLedgerPage> GetGeneralLedgerPageAsync(
        Guid companyId,
        GeneralLedgerFilter filter,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(companyId, filter)
            .OrderBy(g => g.PostingDate)
            .ThenBy(g => g.Id)
            .ToList();

        // Totals over the WHOLE filtered set - the pinned contract the handler must not recompute
        // from the truncated page.
        var totalDebit = filtered.Sum(g => g.Debit);
        var totalCredit = filtered.Sum(g => g.Credit);

        return Task.FromResult(new GeneralLedgerPage(
            filtered.Take(take).ToList(),
            totalDebit,
            totalCredit));
    }

    public Task<IReadOnlyList<AccountBalanceRow>> GetAccountBalancesAsync(
        Guid companyId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var window = _rows
            .Where(g => g.CompanyId == companyId)
            .Where(g => from is not { } lower || g.PostingDate >= lower)
            .Where(g => to is not { } upper || g.PostingDate <= upper)
            .GroupBy(g => g.AccountId);

        var balances = new List<AccountBalanceRow>();
        foreach (var group in window)
        {
            var account = group.Select(g => g.Account).FirstOrDefault(a => a is not null)
                ?? throw new InvalidOperationException(
                    $"Ledger row of account {group.Key} was seeded without its Account navigation.");

            balances.Add(new AccountBalanceRow(
                account.Id,
                account.AccountCode,
                account.AccountName,
                account.RootType,
                account.Type,
                group.Sum(g => g.Debit),
                group.Sum(g => g.Credit)));
        }

        return Task.FromResult<IReadOnlyList<AccountBalanceRow>>(balances);
    }

    /// <summary>Applies the AND-combined report filters (same predicates as the EF repository).</summary>
    private IEnumerable<GLEntry> ApplyFilter(Guid companyId, GeneralLedgerFilter filter) =>
        _rows
            .Where(g => g.CompanyId == companyId)
            .Where(g => filter.AccountId is not { } accountId || g.AccountId == accountId)
            .Where(g => filter.VoucherId is not { } voucherId || g.VoucherId == voucherId)
            .Where(g => string.IsNullOrWhiteSpace(filter.VoucherType)
                || g.VoucherType == filter.VoucherType)
            .Where(g => filter.From is not { } from || g.PostingDate >= from)
            .Where(g => filter.To is not { } to || g.PostingDate <= to);
}

