-- =============================================================================
-- Dev-only seed: Fixed Assets & Depreciation (module 07-assets, task 10.7)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so integration tests and manual curl
-- sessions can reference fixed ids:
--   Tenant     : 11111111-1111-4111-8111-111111111111 (scripts/seed-dev-coa.sql)
--   Company    : 22222222-2222-4222-8222-222222222222 (scripts/seed-dev-coa.sql)
--   Accounts   : a0000000-0000-4000-8000-00000000<1510|1520|5310|4220|5320>
--   Category   : c0000000-0000-4000-8000-000000000001 (IT Hardware)
--   Asset      : d0000000-0000-4000-8000-000000000001 (AST-2026-00001, Laptop)
--
-- Account is system-versioned (Constitution Article IV.2): PeriodStart/PeriodEnd
-- are GENERATED ALWAYS columns and must NOT be listed on INSERT.
-- RootType is stored as the enum NAME (AccountConfiguration.HasConversion<string>).
-- AssetCategory, Asset are system-versioned (plan.md §1 DDL).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Missing GL Accounts for Asset Module ------------------------------------
-- 1510 Fixed Asset Equipment (Asset)
-- 1520 Accumulated Depreciation (Asset, contra)
-- 5310 Depreciation Expense (Expense)
-- 4220 Gain on Asset Disposal (Income)
-- 5320 Loss on Asset Disposal (Expense)

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001510')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001510',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            '1510', 'Fixed Asset Equipment', 'Asset', 0,
            'a0000000-0000-4000-8000-000000001000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001520')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001520',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            '1520', 'Accumulated Depreciation', 'Asset', 0,
            'a0000000-0000-4000-8000-000000001000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000005310')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000005310',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            '5310', 'Depreciation Expense', 'Expense', 0,
            'a0000000-0000-4000-8000-000000005000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000004220')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000004220',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            '4220', 'Gain on Asset Disposal', 'Income', 0,
            'a0000000-0000-4000-8000-000000004000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000005320')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000005320',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            '5320', 'Loss on Asset Disposal', 'Expense', 0,
            'a0000000-0000-4000-8000-000000005000', 1);
GO

-- --- Asset Category: IT Hardware ---------------------------------------------
-- Links to the 6 accounts above + CWIP (2120 from seed-dev-coa.sql)
IF NOT EXISTS (SELECT 1 FROM dbo.AssetCategory WHERE Id = 'c0000000-0000-4000-8000-000000000001')
BEGIN
    INSERT INTO dbo.AssetCategory (Id, TenantId, CompanyId, CategoryName,
        FixedAssetAccountId, AccumulatedDepreciationAccountId, DepreciationExpenseAccountId,
        CwipAccountId, GainOnDisposalAccountId, LossOnDisposalAccountId,
        IsNonDepreciable, IsActive, CreatedAt)
    VALUES ('c0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'IT Hardware',
            'a0000000-0000-4000-8000-000000001510',  -- Fixed Asset Equipment
            'a0000000-0000-4000-8000-000000001520',  -- Accumulated Depreciation
            'a0000000-0000-4000-8000-000000005310',  -- Depreciation Expense
            'a0000000-0000-4000-8000-000000002120',  -- CWIP (Stock Received But Not Billed)
            'a0000000-0000-4000-8000-000000004220',  -- Gain on Disposal
            'a0000000-0000-4000-8000-000000005320',  -- Loss on Disposal
            0, 1, SYSDATETIMEOFFSET());
END;
GO

-- --- Sample Asset: AST-2026-00001 (Dell Precision Laptop) --------------------
-- $2,400 gross, $0 salvage, 24 months, AvailableForUseDate = 2026-10-01
-- Status = Capitalized (schedule generated by capitalization command via live API in test)
IF NOT EXISTS (SELECT 1 FROM dbo.Asset WHERE Id = 'd0000000-0000-4000-8000-000000000001')
BEGIN
    INSERT INTO dbo.Asset (Id, TenantId, CompanyId, AssetCode, AssetName, ItemId, AssetCategoryId,
        PurchaseDate, AvailableForUseDate, GrossPurchaseAmount, SalvageValue,
        AccumulatedDepreciation, DepreciationMethod, TotalNumberOfDepreciations,
        FrequencyInMonths, Status, DisposalDate, CreatedAt)
    VALUES ('d0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'AST-2026-00001', 'Dell Precision Laptop',
            'e0000000-0000-4000-8000-000000000001',  -- ItemId (seeded per-test in integration tests)
            'c0000000-0000-4000-8000-000000000001',  -- IT Hardware category
            '2026-09-15', '2026-10-01', 2400.0000, 0.0000,
            0.0000, 'StraightLine', 24, 1,
            'Capitalized', NULL, SYSDATETIMEOFFSET());
END;
GO

-- Verification
SELECT AccountCode, AccountName, RootType, IsGroup
FROM dbo.Account
WHERE TenantId = '11111111-1111-4111-8111-111111111111'
  AND AccountCode IN ('1510','1520','5310','4220','5320')
ORDER BY AccountCode;

SELECT CategoryName, FixedAssetAccountId, AccumulatedDepreciationAccountId,
       DepreciationExpenseAccountId, CwipAccountId, GainOnDisposalAccountId, LossOnDisposalAccountId
FROM dbo.AssetCategory
WHERE Id = 'c0000000-0000-4000-8000-000000000001';

SELECT AssetCode, AssetName, GrossPurchaseAmount, SalvageValue, TotalNumberOfDepreciations,
       Status, DisposalDate
FROM dbo.Asset
WHERE Id = 'd0000000-0000-4000-8000-000000000001';
GO