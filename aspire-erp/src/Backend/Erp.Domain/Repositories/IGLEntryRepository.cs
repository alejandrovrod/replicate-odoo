using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the READ side of the General Ledger (tasks.md 2.5 - Financial
/// Reporting Queries): the paginated general-ledger rows with their full-set totals, and the
/// per-account Debit/Credit aggregates the trial balance, balance sheet and profit &amp; loss are
/// projected from (plan.md §4 canonical Trial Balance SQL). Implemented by
/// Erp.Infrastructure.Data.Repositories.GLEntryRepository.
/// </summary>
/// <remarks>
/// <para><b>Read-only by design.</b> Constitution Article III.2 makes GLEntry append-only, and
/// every append already flows through <see cref="IJournalRepository"/>/<see cref="IStockRepository"/>
/// /<see cref="IPurchaseRepository"/> inside their posting transactions - so this interface exposes
/// NO write method: a report can never become a way to mutate the ledger.</para>
/// <para><b>Tenant isolation is AUTOMATIC</b> (Constitution II.3): implementations query through
/// AppDbContext, whose global query filter scopes every read to the current tenant. Manual
/// <c>.Where(e =&gt; e.TenantId == ...)</c> in the implementation is forbidden; the
/// <c>CompanyId</c> predicates are business scoping (a report belongs to one company), not
/// tenancy.</para>
/// </remarks>
public interface IGLEntryRepository
{
    /// <summary>
    /// One chronological page of the general ledger for <paramref name="companyId"/> plus the
    /// Debit/Credit totals of the FULL filtered set - the pinned Task 2.6 contract requires the
    /// totals to describe every matching row even when <c>take</c> truncates <c>items</c>.
    /// </summary>
    /// <param name="companyId">Company that owns the ledger rows.</param>
    /// <param name="filter">AND-combined row filters (account, voucher, date range).</param>
    /// <param name="take">Maximum rows in <see cref="GeneralLedgerPage.Items"/> (already clamped by the caller).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    Task<GeneralLedgerPage> GetGeneralLedgerPageAsync(
        Guid companyId,
        GeneralLedgerFilter filter,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One <see cref="AccountBalanceRow"/> per account that MOVED in the window
    /// (<c>from &lt;= PostingDate &lt;= to</c>, both inclusive and optional), carrying the account
    /// identity the report rows display and its <c>SUM(Debit)</c>/<c>SUM(Credit)</c>.
    /// </summary>
    /// <remarks>
    /// Mirrors plan.md §4: the aggregate is grouped per account through a JOIN on Account, so an
    /// account without ledger movement is simply absent from the result. Reversal rows are NOT
    /// excluded - they are part of the ledger and a cancelled voucher nets to exactly zero.
    /// </remarks>
    /// <param name="companyId">Company that owns the ledger rows.</param>
    /// <param name="from">Inclusive lower bound on PostingDate, or null for "from the beginning".</param>
    /// <param name="to">Inclusive upper bound on PostingDate, or null for "up to today".</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    Task<IReadOnlyList<AccountBalanceRow>> GetAccountBalancesAsync(
        Guid companyId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AND-combined filters of the general-ledger report (pinned Task 2.6 contract): every populated
/// member narrows the result, null members are ignored.
/// </summary>
/// <param name="AccountId">Exact posting account, or null for all accounts.</param>
/// <param name="VoucherId">Exact source-document id (voucher drill-down), or null.</param>
/// <param name="VoucherType">Exact source-document type, e.g. "JournalEntry", or null/blank.</param>
/// <param name="From">Inclusive lower bound on PostingDate, or null.</param>
/// <param name="To">Inclusive upper bound on PostingDate, or null.</param>
public sealed record GeneralLedgerFilter(
    Guid? AccountId,
    Guid? VoucherId,
    string? VoucherType,
    DateOnly? From,
    DateOnly? To);

/// <summary>
/// One page of the general-ledger report: the chronological row slice (PostingDate ASC, Id ASC)
/// and the totals of the FULL filtered set it belongs to.
/// </summary>
/// <remarks>
/// <paramref name="Items"/> are full <see cref="GLEntry"/> rows with <see cref="GLEntry.Account"/>
/// loaded, because the report shows the account code/name/root type next to every amount. The
/// totals are computed by the database over ALL matching rows, never over this page - that is the
/// pinned contract the audit viewer's footer relies on when <c>take</c> truncates the list.
/// </remarks>
/// <param name="Items">Page of ledger rows, ordered by PostingDate ASC then Id ASC.</param>
/// <param name="TotalDebit">SUM(Debit) over every row matching the filter.</param>
/// <param name="TotalCredit">SUM(Credit) over every row matching the filter.</param>
public sealed record GeneralLedgerPage(
    IReadOnlyList<GLEntry> Items,
    decimal TotalDebit,
    decimal TotalCredit);

/// <summary>
/// Per-account ledger aggregate (plan.md §4): the account identity a report row displays plus the
/// summed Debit/Credit columns it is derived from.
/// </summary>
/// <remarks>
/// Deliberately carries the RAW <c>SUM(Debit)</c>/<c>SUM(Credit)</c> instead of a signed balance:
/// each report decides the presentation sign for itself (trial balance = Debit − Credit for every
/// account; balance sheet/P&amp;L = the natural sign of the account's RootType). Keeping the
/// aggregate unsigned is what lets ONE repository call serve all three reports.
/// </remarks>
/// <param name="AccountId">Posting account the sums belong to.</param>
/// <param name="AccountCode">Display code, e.g. "1110".</param>
/// <param name="AccountName">Display name.</param>
/// <param name="RootType">Asset / Liability / Equity / Income / Expense (section selector).</param>
/// <param name="Type">ERPNext-parity sub-classification (COGS vs operating expense in the P&amp;L).</param>
/// <param name="TotalDebit">SUM(Debit) of the account inside the window.</param>
/// <param name="TotalCredit">SUM(Credit) of the account inside the window.</param>
public sealed record AccountBalanceRow(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    AccountRootType RootType,
    AccountType Type,
    decimal TotalDebit,
    decimal TotalCredit);
