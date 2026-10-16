using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// A vendor the company buys from (ubiquitous language: "Supplier" - .specify/spec.md §6:
/// Buying Cycle <c>Supplier -&gt; PurchaseOrder -&gt; PurchaseReceipt -&gt; PurchaseInvoice</c>).
/// </summary>
/// <remarks>
/// Tenant-scoped MASTER entity, shared by every company of the tenant (same decision as
/// <see cref="Item"/>). NOT system-versioned: Constitution Article IV.2 lists Account and Company
/// only, and Phase 3 extended that reading to every other master (seed-dev-stock.sql) - Supplier
/// follows the precedent. Codes are unique per TENANT (Task 4.1 acceptance).
/// </remarks>
public class Supplier : ITenantEntity, ICustomFieldsCarrier
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Supplier code, unique per tenant (max 50 chars - see <see cref="PurchaseValidator.MaxCodeLength"/>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (max 150 chars).</summary>
    public string Name { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// Classification (spec §1 gap D1, ERPNext <c>supplier_group</c>). Nullable so the migration is
    /// non-destructive for existing rows; new suppliers should always carry one.
    /// </summary>
    public Guid? SupplierGroupId { get; set; }

    public SupplierGroup? SupplierGroup { get; set; }

    /// <summary>
    /// Legacy single payable account (kept for backward compatibility). Multi-company tenants
    /// should configure the per-company mapping in <see cref="Accounts"/> instead.
    /// </summary>
    public Guid? DefaultPayableAccountId { get; set; }

    public Account? DefaultPayableAccount { get; set; }

    /// <summary>ERPNext parity: default payment-terms template (spec §1 gap D3). Optional.</summary>
    public Guid? PaymentTermsTemplateId { get; set; }

    /// <summary>ERPNext parity: default purchase price list (spec §1 gap D3). Optional.</summary>
    public Guid? DefaultPriceListId { get; set; }

    /// <summary>
    /// Billing currency (RM-09 FK to the global <see cref="Currency"/> catalog);
    /// null means "USD" for legacy rows.
    /// </summary>
    public Guid? CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public int PaymentTermsDays { get; set; } = 30;

    public decimal OutstandingAmount { get; set; }

    /// <summary>Inactive suppliers cannot be referenced by new purchase orders.</summary>
    public bool IsActive { get; set; } = true;

    // --- Governance (spec §1 gap D4) -------------------------------------------------------------

    /// <summary>
    /// ERPNext <c>is_frozen</c>: the supplier record itself is frozen (blocks edits), distinct from
    /// the transactional <see cref="OnHold"/> hold.
    /// </summary>
    public bool IsFrozen { get; set; }

    /// <summary>ERPNext <c>on_hold</c>: the supplier is blocked for the actions in <see cref="HoldType"/>.</summary>
    public bool OnHold { get; set; }

    /// <summary>Which buying documents the hold blocks (meaningful only while <see cref="OnHold"/> is true).</summary>
    public SupplierHoldType HoldType { get; set; } = SupplierHoldType.None;

    /// <summary>
    /// Automatic release date (spec scenario SUP-G3): once the current date passes it the hold no
    /// longer applies (see <see cref="IsBlockedFor"/>). Null means the hold never auto-expires.
    /// </summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>
    /// Per-company payable/advance account mapping (spec §1 gap D2). Unique per company at the
    /// database; managed through <see cref="SetCompanyAccount"/>.
    /// </summary>
    public ICollection<SupplierCompanyAccount> Accounts { get; set; } = new List<SupplierCompanyAccount>();

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): EF puts the original value
    /// in the UPDATE ... WHERE clause, so a concurrent change between load and save throws
    /// <c>DbUpdateConcurrencyException</c> instead of silently winning.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>Tenant-configured dynamic attributes, see <see cref="ICustomFieldsCarrier"/>.</summary>
    public string? CustomData { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // --- Aggregate behaviour (spec §3) ----------------------------------------------------------

    /// <summary>
    /// True when the supplier must be rejected for <paramref name="requestedAction"/> on
    /// <paramref name="currentDate"/> (spec §3, scenarios SUP-G2/SUP-G3).
    /// </summary>
    /// <remarks>
    /// An inactive supplier is always blocked. A hold is bypassed once <see cref="ReleaseDate"/> has
    /// elapsed (strictly past, so a release date equal to the posting date still blocks - spec
    /// SUP-G3 releases only AFTER the date). Otherwise the hold applies when it targets EVERY action
    /// (<see cref="SupplierHoldType.All"/>) or the requested one.
    /// </remarks>
    public bool IsBlockedFor(SupplierHoldType requestedAction, DateOnly currentDate)
    {
        if (!IsActive)
        {
            return true;
        }

        if (!OnHold)
        {
            return false;
        }

        if (ReleaseDate.HasValue && currentDate > ReleaseDate.Value)
        {
            return false;
        }

        return HoldType == SupplierHoldType.All || HoldType == requestedAction;
    }

    /// <summary>Sets the hold status (spec T1.2 <c>SetHoldStatus</c>). Releasing clears type and date.</summary>
    public void SetHoldStatus(bool onHold, SupplierHoldType holdType, DateOnly? releaseDate = null)
    {
        OnHold = onHold;
        HoldType = onHold ? holdType : SupplierHoldType.None;
        ReleaseDate = onHold ? releaseDate : null;
    }

    /// <summary>
    /// Creates or updates the per-company account mapping (spec §3 <c>SetCompanyAccount</c>): the
    /// aggregate owns the uniqueness rule (one row per company).
    /// </summary>
    public void SetCompanyAccount(Guid companyId, Guid payableAccountId, Guid? advanceAccountId = null)
    {
        var existing = Accounts.FirstOrDefault(a => a.CompanyId == companyId);
        if (existing is not null)
        {
            existing.Update(payableAccountId, advanceAccountId);
            return;
        }

        Accounts.Add(new SupplierCompanyAccount
        {
            Id = Guid.NewGuid(),
            SupplierId = Id,
            CompanyId = companyId,
            PayableAccountId = payableAccountId,
            AdvanceAccountId = advanceAccountId,
        });
    }
}
