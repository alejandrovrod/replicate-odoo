using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum ValuationMethod
{
    Fifo,
    MovingAverage,
    Lifo,
}

public class Item : ITenantEntity, ICustomFieldsCarrier
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

    /// <summary>
    /// Batch-tracking flag (batch-and-serial Fase 3, gap D1): every stock movement line for
    /// this item must name a <see cref="Batch"/> (receipts create-or-top-up by number, issues
    /// and transfers consume by id with FEFO guidance). Coexists with
    /// <see cref="HasSerialNo"/> - a line needing both names a batch AND the exact serials.
    /// </summary>
    public bool HasBatchNo { get; set; }

    /// <summary>
    /// Serial-tracking flag (batch-and-serial Fase 3, gap D1): every moved unit must be
    /// identified by serial number (count must equal the line quantity exactly).
    /// </summary>
    public bool HasSerialNo { get; set; }

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

    /// <summary>Tenant-configured dynamic attributes, see <see cref="ICustomFieldsCarrier"/>.</summary>
    public string? CustomData { get; set; }
}
