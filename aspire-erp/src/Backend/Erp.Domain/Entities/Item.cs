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

    /// <summary>Default income account for sales postings (nullable FK to Account).</summary>
    public Guid? IncomeAccountId { get; set; }

    public Account? IncomeAccount { get; set; }

    /// <summary>Default expense account for purchase/expense postings (nullable FK to Account).</summary>
    public Guid? ExpenseAccountId { get; set; }

    public Account? ExpenseAccount { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}
