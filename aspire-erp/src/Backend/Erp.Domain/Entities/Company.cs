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
    /// Hard period lock (plan.md §3.2): documents dated on or before this day cannot be posted.
    /// Added in Phase 3 to kill schema drift; enforced by later phases.
    /// </summary>
    public DateOnly? PeriodLockDate { get; set; }

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

    public DateTimeOffset CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
