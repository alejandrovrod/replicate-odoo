using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Apportionment strategy of a landed cost voucher (spec D2): by receipt-line value
/// (<c>Amount</c>) or by received quantity (<c>Quantity</c>). One strategy per voucher.
/// Persisted as the enum NAME (nvarchar), matching the codebase precedent.
/// </summary>
public enum LandedCostDistributeBy
{
    /// <summary>Weight = receipt line amount (value-proportional, LCV-G1).</summary>
    Amount = 1,

    /// <summary>Weight = receipt line quantity (LCV-G2).</summary>
    Quantity = 2,
}

/// <summary>
/// Landed cost voucher header (Fase 4, gap D1): retroactive acquisition charges
/// (freight, insurance, customs) apportioned over submitted purchase receipts.
/// Lifecycle Draft → Submitted → Cancelled (<see cref="DocumentStatus"/>), gapless
/// <c>LCV-YYYY-NNNNN</c> assigned inside the submit transaction.
/// </summary>
/// <remarks>
/// No <c>AggregateRoot&lt;T&gt;</c> base exists in this codebase (all aggregates implement
/// <see cref="ITenantEntity"/> directly), so this entity follows the same shape.
/// </remarks>
public class LandedCostVoucher : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Gapless voucher number, assigned at submit; empty while Draft.</summary>
    public string VoucherNo { get; set; } = string.Empty;

    /// <summary>Accounting date of the adjustment (and of the GL/Kardex rows it produces).</summary>
    public DateOnly PostingDate { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public LandedCostDistributeBy DistributeBy { get; set; } = LandedCostDistributeBy.Amount;

    /// <summary>Total additional charges (exact sum of <see cref="Charges"/>).</summary>
    public decimal TotalTaxesAndCharges { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): submit/cancel carry the
    /// client token for compare-and-swap. Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Receipts the charges are spread over (Submitted only - LCV-G4).</summary>
    public ICollection<LandedCostReceiptReference> Receipts { get; set; } = new List<LandedCostReceiptReference>();

    /// <summary>Freight/insurance/customs charges with their bridge/expense accounts.</summary>
    public ICollection<LandedCostChargeItem> Charges { get; set; } = new List<LandedCostChargeItem>();

    /// <summary>Pre-calculated apportionment per receipt line (draft-time preview + submit source).</summary>
    public ICollection<LandedCostApportionmentItem> Items { get; set; } = new List<LandedCostApportionmentItem>();
}

/// <summary>One receipt linked to a landed cost voucher.</summary>
public class LandedCostReceiptReference
{
    public Guid Id { get; set; }

    public Guid LandedCostVoucherId { get; set; }

    public LandedCostVoucher? LandedCostVoucher { get; set; }

    public Guid PurchaseReceiptId { get; set; }

    public PurchaseReceipt? PurchaseReceipt { get; set; }
}

/// <summary>One additional charge (freight, insurance, customs) with its GL account.</summary>
public class LandedCostChargeItem
{
    public Guid Id { get; set; }

    public Guid LandedCostVoucherId { get; set; }

    public LandedCostVoucher? LandedCostVoucher { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Charge amount (decimal(18,4), strictly positive).</summary>
    public decimal Amount { get; set; }

    /// <summary>Bridge/expense account credited at submit (e.g. freight clearing).</summary>
    public Guid ExpenseAccountId { get; set; }

    public Account? ExpenseAccount { get; set; }
}

/// <summary>Pre-calculated apportionment of the charges onto one receipt line.</summary>
public class LandedCostApportionmentItem
{
    public Guid Id { get; set; }

    public Guid LandedCostVoucherId { get; set; }

    public LandedCostVoucher? LandedCostVoucher { get; set; }

    public Guid PurchaseReceiptId { get; set; }

    public PurchaseReceipt? PurchaseReceipt { get; set; }

    public Guid PurchaseReceiptLineId { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Received quantity the charges spread over (from the receipt line).</summary>
    public decimal Qty { get; set; }

    /// <summary>Receipt unit rate before landed costs.</summary>
    public decimal OriginalRate { get; set; }

    /// <summary>Charges apportioned to this line (exact cents, residual-absorbed).</summary>
    public decimal ApplicableCharges { get; set; }

    /// <summary>New valuation rate: (Qty × OriginalRate + ApplicableCharges) / Qty.</summary>
    public decimal NewRate { get; set; }
}
