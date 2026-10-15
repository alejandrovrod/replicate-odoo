using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum ValuationMethod
{
    Fifo,
    MovingAverage,
    Lifo,
}

public class Item : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid StockUomId { get; set; }
    public UOM? StockUom { get; set; }

    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.Fifo;

    public bool IsStockItem { get; set; } = true;

    public Guid? DefaultWarehouseId { get; set; }
    public Warehouse? DefaultWarehouse { get; set; }

    public decimal StandardSellingRate { get; set; }
    public decimal SafetyStock { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Fixed-asset flag (asset-capitalization module, gap D1). When true, the item is capitalized
    /// on the vendor bill: the posting debits the category's cost account and the handler creates
    /// an <see cref="Asset"/> linked to the invoice, instead of leaving the value expensed.
    /// </summary>
    public bool IsFixedAsset { get; set; }

    /// <summary>
    /// Asset category driving the capitalization accounts (cost, depreciation, accumulated) for a
    /// fixed-asset item. REQUIRED when <see cref="IsFixedAsset"/> is true; unused otherwise.
    /// </summary>
    public Guid? AssetCategoryId { get; set; }

    public AssetCategory? AssetCategory { get; set; }

    /// <summary>Default income account for sales postings (nullable FK to Account).</summary>
    public Guid? IncomeAccountId { get; set; }

    public Account? IncomeAccount { get; set; }

    /// <summary>Default expense account for purchase/expense postings (nullable FK to Account).</summary>
    public Guid? ExpenseAccountId { get; set; }

    public Account? ExpenseAccount { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}
