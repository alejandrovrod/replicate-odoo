# Technical Plan: Stock & Perpetual Inventory (ERPNext Parity)

**Module:** `02-stock`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, Perpetual Valuation, FIFO Costing Engine  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Unit of Measure (UOM)
CREATE TABLE UOM (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    UomName NVARCHAR(50) NOT NULL,
    Symbol NVARCHAR(10) NOT NULL,
    MustBeWholeNumber BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);

-- 2. Item Master (Stock SKUs)
CREATE TABLE Item (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ItemCode NVARCHAR(100) NOT NULL,
    ItemName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    StockUomId UNIQUEIDENTIFIER NOT NULL,
    ValuationMethod NVARCHAR(20) NOT NULL DEFAULT 'FIFO',
    IsStockItem BIT NOT NULL DEFAULT 1,
    DefaultWarehouseId UNIQUEIDENTIFIER NULL,
    StandardSellingRate DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    SafetyStock DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    IsActive BIT NOT NULL DEFAULT 1,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CONSTRAINT CK_Item_SellingRate CHECK (StandardSellingRate >= 0.0000),
    CONSTRAINT CK_Item_SafetyStock CHECK (SafetyStock >= 0.0000),
    CONSTRAINT FK_Item_Uom FOREIGN KEY (StockUomId) REFERENCES UOM(Id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.ItemHistory));

CREATE UNIQUE NONCLUSTERED INDEX UQ_Item_Tenant_Company_Code 
ON Item (TenantId, CompanyId, ItemCode);

-- 3. Warehouse Hierarchy
CREATE TABLE Warehouse (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    WarehouseCode NVARCHAR(50) NOT NULL,
    WarehouseName NVARCHAR(150) NOT NULL,
    ParentWarehouseId UNIQUEIDENTIFIER NULL,
    IsGroup BIT NOT NULL DEFAULT 0,
    Type NVARCHAR(30) NOT NULL DEFAULT 'Physical', -- Physical, Transit, WIP, Scrap
    AccountId UNIQUEIDENTIFIER NULL, -- Linked Stock Asset Account in COA
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Warehouse_Parent FOREIGN KEY (ParentWarehouseId) REFERENCES Warehouse(Id),
    CONSTRAINT FK_Warehouse_Account FOREIGN KEY (AccountId) REFERENCES Account(Id)
);

-- 4. Stock Entry (Material Movement Header)
CREATE TABLE StockEntry (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    EntryNumber NVARCHAR(50) NOT NULL,
    Purpose NVARCHAR(50) NOT NULL, -- MaterialReceipt, MaterialIssue, MaterialTransfer, Manufacture
    PostingDate DATE NOT NULL,
    PostingTime TIME NOT NULL,
    Status INT NOT NULL DEFAULT 1, -- Draft, Submitted, Cancelled
    FromWarehouseId UNIQUEIDENTIFIER NULL,
    ToWarehouseId UNIQUEIDENTIFIER NULL,
    TotalAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_StockEntry_TotalAmount CHECK (TotalAmount >= 0.0000)
);

-- 5. Stock Entry Item (Movement Lines)
CREATE TABLE StockEntryItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    StockEntryId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    SourceWarehouseId UNIQUEIDENTIFIER NULL,
    TargetWarehouseId UNIQUEIDENTIFIER NULL,
    Quantity DECIMAL(18,4) NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    BasicRate DECIMAL(18,4) NOT NULL,
    Amount DECIMAL(18,4) NOT NULL,
    CONSTRAINT CK_StockEntryItem_Quantity CHECK (Quantity > 0.0000),
    CONSTRAINT CK_StockEntryItem_Rate CHECK (BasicRate >= 0.0000),
    CONSTRAINT CK_StockEntryItem_Amount CHECK (Amount >= 0.0000),
    CONSTRAINT FK_StockEntryItem_Header FOREIGN KEY (StockEntryId) REFERENCES StockEntry(Id) ON DELETE CASCADE,
    CONSTRAINT FK_StockEntryItem_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- 6. Stock Ledger Entry (Immutable Physical Kardex)
CREATE TABLE StockLedgerEntry (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    PostingDate DATE NOT NULL,
    PostingDateTime DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    ItemId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    ActualQty DECIMAL(18,4) NOT NULL,
    QtyAfterTransaction DECIMAL(18,4) NOT NULL,
    IncomingRate DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    ValuationRate DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    StockValue DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    StockValueDifference DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    VoucherType NVARCHAR(50) NOT NULL,
    VoucherNo NVARCHAR(100) NOT NULL,
    VoucherId UNIQUEIDENTIFIER NOT NULL,
    IsCancelled BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_SLE_ActualQty CHECK (ActualQty != 0.0000),
    CONSTRAINT CK_SLE_IncomingRate CHECK (IncomingRate >= 0.0000),
    CONSTRAINT CK_SLE_ValuationRate CHECK (ValuationRate >= 0.0000),
    CONSTRAINT FK_SLE_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_SLE_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
);

CREATE NONCLUSTERED INDEX IX_SLE_Tenant_Item_Warehouse_Date 
ON StockLedgerEntry (TenantId, CompanyId, ItemId, WarehouseId, PostingDateTime)
INCLUDE (ActualQty, QtyAfterTransaction, ValuationRate, StockValue);
```

---

## 2. Domain Error Catalog & Exception Contracts

```csharp
namespace Erp.Domain.Stock.Errors;

public static class StockErrorCodes
{
    public const string InsufficientStock = "STOCK_INSUFFICIENT_QTY";
    public const string NegativeStockProhibited = "STOCK_NEGATIVE_PROHIBITED";
    public const string IdenticalWarehouseTransfer = "STOCK_IDENTICAL_WAREHOUSES";
    public const string ItemInactive = "STOCK_ITEM_INACTIVE";
    public const string WarehouseInactive = "STOCK_WAREHOUSE_INACTIVE";
    public const string PeriodLocked = "STOCK_PERIOD_LOCKED";
    public const string DuplicateSubmission = "STOCK_DUPLICATE_SUBMISSION";
    public const string ConcurrencyConflict = "STOCK_CONCURRENCY_CONFLICT";
}
```

---

## 3. CQRS Commands & Queries

```csharp
namespace Erp.Application.Features.Stock.Commands;

public record PostStockEntryCommand(
    Guid TenantId,
    Guid CompanyId,
    string EntryNumber,
    string Purpose,
    DateTime PostingDate,
    Guid? FromWarehouseId,
    Guid? ToWarehouseId,
    List<StockEntryItemDto> Items,
    string IdempotencyKey
) : IRequest<StockEntryResultDto>;

public record CancelStockEntryCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid StockEntryId,
    string Reason
) : IRequest<Unit>;
```

---

## 4. FIFO Valuation Queue Algorithm

```csharp
namespace Erp.Domain.Services;

public sealed class FifoCostEngine
{
    public static (decimal cogsAmount, decimal newUnitValuationRate) ConsumeFifoLayers(
        List<FifoBatchLayer> existingLayers,
        decimal quantityToConsume)
    {
        decimal totalCost = 0.0m;
        decimal remainingQtyToConsume = quantityToConsume;

        while (remainingQtyToConsume > 0)
        {
            if (existingLayers.Count == 0)
            {
                throw new InsufficientStockException("Cannot consume stock: FIFO layers exhausted.");
            }

            var oldestLayer = existingLayers[0];

            if (oldestLayer.Quantity <= remainingQtyToConsume)
            {
                totalCost += oldestLayer.Quantity * oldestLayer.UnitRate;
                remainingQtyToConsume -= oldestLayer.Quantity;
                existingLayers.RemoveAt(0);
            }
            else
            {
                totalCost += remainingQtyToConsume * oldestLayer.UnitRate;
                oldestLayer.Deduct(remainingQtyToConsume);
                remainingQtyToConsume = 0;
            }
        }

        var remainingTotalQty = existingLayers.Sum(l => l.Quantity);
        var remainingTotalValue = existingLayers.Sum(l => l.TotalValue);
        var newUnitRate = remainingTotalQty > 0 ? remainingTotalValue / remainingTotalQty : 0.0m;

        return (totalCost, newUnitRate);
    }
}
```
