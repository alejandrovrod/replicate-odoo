# Technical Plan: Asset Management & Depreciation (ERPNext Parity)

**Module:** `07-assets`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, Automated Amortization, Disposal Balancing  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Asset Category (GL Account Templates)
CREATE TABLE AssetCategory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    CategoryName NVARCHAR(100) NOT NULL,
    FixedAssetAccountId UNIQUEIDENTIFIER NOT NULL,
    AccumulatedDepreciationAccountId UNIQUEIDENTIFIER NOT NULL,
    DepreciationExpenseAccountId UNIQUEIDENTIFIER NOT NULL,
    CwipAccountId UNIQUEIDENTIFIER NULL,
    IsNonDepreciable BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_AssetCategory_FixedAsset FOREIGN KEY (FixedAssetAccountId) REFERENCES Account(Id),
    CONSTRAINT FK_AssetCategory_AccumDep FOREIGN KEY (AccumulatedDepreciationAccountId) REFERENCES Account(Id),
    CONSTRAINT FK_AssetCategory_DepExpense FOREIGN KEY (DepreciationExpenseAccountId) REFERENCES Account(Id)
);

-- 2. Fixed Asset Master
CREATE TABLE Asset (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    AssetCode NVARCHAR(50) NOT NULL,
    AssetName NVARCHAR(150) NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    AssetCategoryId UNIQUEIDENTIFIER NOT NULL,
    PurchaseDate DATE NOT NULL,
    AvailableForUseDate DATE NOT NULL,
    GrossPurchaseAmount DECIMAL(18,4) NOT NULL,
    SalvageValue DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    AccumulatedDepreciation DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    DepreciationMethod NVARCHAR(30) NOT NULL DEFAULT 'StraightLine',
    TotalNumberOfDepreciations INT NOT NULL,
    FrequencyInMonths INT NOT NULL DEFAULT 1,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Draft', -- Draft, Submitted, Capitalized, FullyDepreciated, Sold, Scrapped
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_Asset_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_Asset_Category FOREIGN KEY (AssetCategoryId) REFERENCES AssetCategory(Id)
);

-- 3. Asset Depreciation Schedule (Scheduled Amortization)
CREATE TABLE AssetDepreciationSchedule (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AssetId UNIQUEIDENTIFIER NOT NULL,
    ScheduleDate DATE NOT NULL,
    DepreciationAmount DECIMAL(18,4) NOT NULL,
    AccumulatedDepreciationAfter DECIMAL(18,4) NOT NULL,
    IsBooked BIT NOT NULL DEFAULT 0,
    JournalEntryId UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_DepSchedule_Asset FOREIGN KEY (AssetId) REFERENCES Asset(Id) ON DELETE CASCADE
);
```

---

## 2. Depreciation Schedule Generation Algorithm

```csharp
namespace Erp.Domain.Services;

public sealed class DepreciationScheduler
{
    public static List<AssetDepreciationScheduleLine> GenerateStraightLineSchedule(
        decimal grossAmount,
        decimal salvageValue,
        int totalDepreciations,
        int frequencyInMonths,
        DateOnly availableForUseDate)
    {
        var depreciableAmount = grossAmount - salvageValue;
        var periodicAmount = Math.Round(depreciableAmount / totalDepreciations, 4);
        var schedule = new List<AssetDepreciationScheduleLine>();
        decimal accumulated = 0.0m;

        for (int i = 1; i <= totalDepreciations; i++)
        {
            var scheduleDate = availableForUseDate.AddMonths(i * frequencyInMonths);
            var isLast = i == totalDepreciations;
            var amount = isLast ? (depreciableAmount - accumulated) : periodicAmount;

            accumulated += amount;
            schedule.Add(new AssetDepreciationScheduleLine(scheduleDate, amount, accumulated));
        }

        return schedule;
    }
}
```
