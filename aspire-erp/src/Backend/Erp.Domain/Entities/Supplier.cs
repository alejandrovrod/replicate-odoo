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
public class Supplier : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Supplier code, unique per tenant (max 50 chars - see <see cref="PurchaseValidator.MaxCodeLength"/>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (max 150 chars).</summary>
    public string Name { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public Guid? DefaultPayableAccountId { get; set; }

    public Account? DefaultPayableAccount { get; set; }

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

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): EF puts the original value
    /// in the UPDATE ... WHERE clause, so a concurrent change between load and save throws
    /// <c>DbUpdateConcurrencyException</c> instead of silently winning.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
