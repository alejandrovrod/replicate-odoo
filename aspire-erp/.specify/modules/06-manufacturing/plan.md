# Technical Plan: Manufacturing & Production (ERPNext Parity)

**Module:** `06-manufacturing`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, BOM Costing Engine, WIP Valuation  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Workstation Equipment Master
CREATE TABLE Workstation (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    WorkstationName NVARCHAR(100) NOT NULL,
    HourRateLabor DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    HourRateElectricity DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    HourRateRent DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    HourRateTotal AS (HourRateLabor + HourRateElectricity + HourRateRent),
    IsActive BIT NOT NULL DEFAULT 1
);

-- 2. Bill of Materials (BOM Master)
CREATE TABLE BOM (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    BomNumber NVARCHAR(50) NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    Quantity DECIMAL(18,4) NOT NULL DEFAULT 1.0000,
    UomId UNIQUEIDENTIFIER NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDefault BIT NOT NULL DEFAULT 1,
    RawMaterialCost DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    OperatingCost DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    ScrapCost DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TotalCost DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_BOM_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_BOM_UOM FOREIGN KEY (UomId) REFERENCES UOM(Id)
);

-- 3. BOM Component Item
CREATE TABLE BOMItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    BomId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    Quantity DECIMAL(18,4) NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    ValuationRate DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Amount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    ScrapPercentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT FK_BOMItem_BOM FOREIGN KEY (BomId) REFERENCES BOM(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BOMItem_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- 4. Work Order (Production Authorization)
CREATE TABLE WorkOrder (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    OrderNumber NVARCHAR(50) NOT NULL,
    ProductionItemId UNIQUEIDENTIFIER NOT NULL,
    BomId UNIQUEIDENTIFIER NOT NULL,
    QuantityToProduce DECIMAL(18,4) NOT NULL,
    ProducedQuantity DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Status INT NOT NULL DEFAULT 1, -- Draft, Submitted, InProcess, Completed, Cancelled
    SourceWarehouseId UNIQUEIDENTIFIER NOT NULL,
    WipWarehouseId UNIQUEIDENTIFIER NOT NULL,
    TargetWarehouseId UNIQUEIDENTIFIER NOT NULL,
    PlannedStartDate DATE NOT NULL,
    PlannedEndDate DATE NOT NULL,
    ActualStartDate DATE NULL,
    ActualEndDate DATE NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_WorkOrder_Item FOREIGN KEY (ProductionItemId) REFERENCES Item(Id),
    CONSTRAINT FK_WorkOrder_BOM FOREIGN KEY (BomId) REFERENCES BOM(Id)
);
```

---

## 2. Cost Capitalization Engine

```csharp
namespace Erp.Domain.Services;

public sealed class ManufacturingCostEngine
{
    public static decimal CalculateFinishedUnitCost(
        decimal totalRawMaterialCost,
        decimal totalOperatingCost,
        decimal scrapSalvageValue,
        decimal producedQuantity)
    {
        if (producedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(producedQuantity), "Produced quantity must be positive.");
        }

        var netTotalCost = totalRawMaterialCost + totalOperatingCost - scrapSalvageValue;
        return Math.Round(netTotalCost / producedQuantity, 4);
    }
}
```
