namespace Erp.Domain.Entities;

/// <summary>
/// One component line of a <see cref="BillOfMaterials"/> (Task 9.2, plan.md §1 DDL table 3):
/// how much of which item the recipe consumes, at what valuation rate. <see cref="Amount"/> is
/// the persisted snapshot (qty x rate); <see cref="ScrapPercentage"/> is the salvageable share
/// (plan DDL decimal(5,2), &gt;= 0 only - values above 100 are rejected nowhere because ERPNext
/// allows multi-output scrap above 100, so only negatives are invalid).
/// </summary>
public class BomItem
{
    public Guid Id { get; set; }

    public Guid BomId { get; set; }

    public BillOfMaterials? Bom { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Consumed quantity; strictly positive (CK_BOMItem_Quantity).</summary>
    public decimal Quantity { get; set; }

    public Guid UomId { get; set; }

    public UOM? Uom { get; set; }

    /// <summary>Unit valuation rate, decimal(18,4), must be &gt;= 0.</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>Persisted line snapshot (qty x rate), decimal(18,4), must be &gt;= 0.</summary>
    public decimal Amount { get; set; }

    /// <summary>Scrap share in percent, decimal(5,2), must be &gt;= 0.</summary>
    public decimal ScrapPercentage { get; set; }
}
