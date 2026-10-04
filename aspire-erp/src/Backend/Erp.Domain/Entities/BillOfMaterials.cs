using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Bill of Materials master (Task 9.2, plan.md §1 DDL table 2): the recipe to produce
/// <see cref="Quantity"/> units of <see cref="ItemId"/>. Cost snapshots
/// (<see cref="RawMaterialCost"/>, <see cref="OperatingCost"/>, <see cref="ScrapCost"/>,
/// <see cref="TotalCost"/>) are persisted roll-ups computed by
/// <c>ManufacturingCostEngine.CalculateBomTotals</c>; <c>BomNumber</c> stays a settable string
/// (default "") because gapless numbering is assigned on submit by Block B (Task 9.3/9.4).
/// System-versioned (temporal) per plan DDL.
/// </summary>
public class BillOfMaterials : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string BomNumber { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Finished quantity this BOM yields; strictly positive (CK_BOM_Quantity).</summary>
    public decimal Quantity { get; set; } = 1m;

    public Guid UomId { get; set; }

    public UOM? Uom { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; } = true;

    public decimal RawMaterialCost { get; set; }

    public decimal OperatingCost { get; set; }

    public decimal ScrapCost { get; set; }

    public decimal TotalCost { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<BomItem> Items { get; set; } = new List<BomItem>();

    public ICollection<BomOperation> Operations { get; set; } = new List<BomOperation>();
}
