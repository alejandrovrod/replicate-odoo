using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Inventory valuation strategies (ubiquitous language: "Valuation Method" -
/// .specify/spec.md §4). Only <see cref="Fifo"/> is implemented in Phase 3; the other two
/// values exist so the persisted enum is forward-compatible with later phases.
/// </summary>
public enum ValuationMethod
{
    Fifo,
    MovingAverage,
    Lifo,
}

/// <summary>
/// A stock-keeping unit (SKU) - the thing that is received, issued and transferred
/// (.specify/spec.md §4 Module 3: "Item (SKU)").
/// </summary>
/// <remarks>
/// Tenant-scoped (NOT company-scoped): the Definition of Done for Task 3.1 requires a unique SKU
/// <b>per tenant</b>, so the uniqueness index is (TenantId, Code) and there is no CompanyId.
/// Stock valuation follows the item's <see cref="ValuationMethod"/>; Phase 3 posts FIFO only.
/// </remarks>
public class Item : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>SKU, unique within the tenant (max 50 chars).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (max 150 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the item is valued. Phase 3 implements <see cref="ValuationMethod.Fifo"/> only.</summary>
    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.Fifo;

    /// <summary>Base unit of measure (required): every stock movement is expressed in it.</summary>
    public Guid BaseUOMId { get; set; }

    public UOM? BaseUOM { get; set; }

    /// <summary>Credit account for sales of this item (optional; used by later phases).</summary>
    public Guid? IncomeAccountId { get; set; }

    public Account? IncomeAccount { get; set; }

    /// <summary>Debit account for Cost of Goods Sold when the item is issued (required to post issues).</summary>
    public Guid? ExpenseAccountId { get; set; }

    public Account? ExpenseAccount { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - specs ST-06/BY-06): EF puts
    /// the original value in the UPDATE ... WHERE clause, so a concurrent change made between the
    /// load and the save throws <c>DbUpdateConcurrencyException</c> instead of silently winning.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;
}
