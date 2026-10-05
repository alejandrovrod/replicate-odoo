using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// A legal tax entity operating under a Tenant (ubiquitous language: "Company" - see
/// .specify/domain_business_rules_ddd.md §1). Holds its own Chart of Accounts, currency and tax ID.
/// </summary>
/// <remarks>
/// Tenant-scoped: implements <c>ITenantEntity</c> (Constitution Article II.1). The value is
/// assigned automatically on insert and can never be altered afterwards (Constitution Article II.4).
/// </remarks>
public class Company : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DefaultCurrency { get; set; } = "USD";

    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// Hard period lock (plan.md §2 "FrozenAccountsDate DATE NULL" / spec AC-04): documents dated
    /// on or before this day cannot be posted. NULL = every period open. Enforced by
    /// <see cref="EnsurePostingDateUnlocked"/> at every posting entry point.
    /// </summary>
    public DateOnly? FrozenAccountsDate { get; set; }

    /// <summary>
    /// Spec AC-04 / tasks.md 2.2: rejects a posting dated inside the closed fiscal period. The
    /// canonical plan.md §3 rule: <c>FrozenAccountsDate.HasValue &amp;&amp; postingDate &lt;=
    /// FrozenAccountsDate</c> -&gt; <see cref="FiscalPeriodLockedException"/>.
    /// </summary>
    /// <remarks>
    /// Domain method (Constitution I.2): pure, no I/O, reusable by EVERY posting pipeline - the
    /// stock and purchase services call it before the first GLEntry line is built, and the
    /// JournalEntry pipeline (tasks.md 2.3) calls it the same way.
    /// </remarks>
    /// <param name="postingDate">Accounting date of the voucher being posted.</param>
    /// <exception cref="Exceptions.FiscalPeriodLockedException">
    /// <paramref name="postingDate"/> &lt;= <see cref="FrozenAccountsDate"/>.
    /// </exception>
    public void EnsurePostingDateUnlocked(DateOnly postingDate)
    {
        if (FrozenAccountsDate.HasValue && postingDate <= FrozenAccountsDate.Value)
        {
            throw new Exceptions.FiscalPeriodLockedException(postingDate, FrozenAccountsDate.Value);
        }
    }

    /// <summary>
    /// Company policy for Task 3.3: when false, issuing/transferring more stock than available
    /// throws <see cref="Exceptions.InsufficientStockException"/>. plan.md §3.2 default = false.
    /// </summary>
    public bool AllowNegativeStock { get; set; }

    /// <summary>
    /// Company-level GL default for stock receipts (decision D3): the ACCOUNT CODE credited when
    /// goods are received (spec ST-01: "2120 - Stock Received But Not Billed"). Stored as a plain
    /// code, NOT a foreign key, because a Company -> Account FK would create a circular table
    /// dependency (Account already references Company through FK_Account_Company). Resolved at
    /// posting time to exactly one active leaf account of the same company.
    /// </summary>
    public string? StockReceivedAccountCode { get; set; }

    /// <summary>
    /// Company-level GL default for vendor bills (Tasks 4.3, same decision D3 code-not-FK shape as
    /// <see cref="StockReceivedAccountCode"/>): the ACCOUNT CODE credited with the gross payable,
    /// e.g. "2110 - Accounts Payable". Debited indirectly: the invoice credits it for
    /// net + Input Tax (spec BY-01).
    /// </summary>
    public string? AccountsPayableAccountCode { get; set; }

    /// <summary>
    /// Company-level GL default for recoverable input tax (decision D3, Task 4.3): the ACCOUNT
    /// CODE debited with <c>PurchaseInvoice.TaxAmount</c>, e.g. "1130 - Input Tax Recoverable"
    /// (spec BY-01: Debit Input Tax Recoverable $100.00).
    /// </summary>
    public string? InputTaxRecoverableAccountCode { get; set; }

    /// <summary>
    /// Company-level GL default for purchase price variances (decision D3, Task 4.3): the ACCOUNT
    /// CODE that absorbs the difference when the billed rate differs from the received rate,
    /// e.g. "5120 - Purchase Price Difference". Only resolved when a variance actually exists.
    /// </summary>
    public string? PriceDifferenceAccountCode { get; set; }

    /// <summary>
    /// Company-level GL default for cost of goods sold (decision D3, spec ST-02): the ACCOUNT CODE
    /// debited with the FIFO consumption value when stock is issued, e.g. "5210 - Cost of Goods
    /// Sold". The item-level ExpenseAccountId was removed by the stock entity refactor: issues
    /// resolve this company default at posting time to exactly one active leaf account of the
    /// same company (same shape as <see cref="StockReceivedAccountCode"/>).
    /// </summary>
    public string? CogsAccountCode { get; set; }

    public string? DefaultReceivableAccountCode { get; set; }
    
    public string? DefaultIncomeAccountCode { get; set; }

    /// <summary>
    /// Company-level GL default for net salary obligations (decision D3, Tasks 12.3-12.4, spec
    /// HR-02): the ACCOUNT CODE credited with <c>PayrollEntry.TotalNetPay</c> on accrual and
    /// debited back to zero on disbursement, e.g. "2150 - Payroll Payable". Same code-not-FK
    /// shape as <see cref="StockReceivedAccountCode"/> (a Company -&gt; Account FK would be
    /// circular). The Block C migration adds the column; until then the entity carries the
    /// property and the EF mapping, and postings resolve it through
    /// <c>IAccountRepository.FindActiveLeafByCodeAsync</c>.
    /// </summary>
    public string? PayrollPayableAccountCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
