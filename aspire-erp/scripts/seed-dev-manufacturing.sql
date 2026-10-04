-- =============================================================================
-- Dev-only seed: Manufacturing & Production (module 06-manufacturing, task 9.7)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so integration tests and manual curl
-- sessions can reference fixed ids:
--   Tenant     : 11111111-1111-4111-8111-111111111111 (scripts/seed-dev-coa.sql)
--   Company    : 22222222-2222-4222-8222-222222222222 (scripts/seed-dev-coa.sql)
--   Accounts   : a0000000-0000-4000-8000-00000000<1320|1330>
--   Warehouse  : d0000000-0000-4000-8000-000000000004 (WIP-01, linked to 1320)
--   Workstation: e0000000-0000-4000-8000-000000000001 (WS-01, $40/hour)
--
-- Depends on seed-dev-coa.sql (tenant, company, 1310/5210) and seed-dev-stock.sql
-- (WH root + WH-01/WH-02). Account is system-versioned (Constitution Article IV.2):
-- PeriodStart/PeriodEnd are GENERATED ALWAYS columns and must NOT be listed on
-- INSERT. RootType is stored as the enum NAME. Workstation.HourRateTotal is a
-- COMPUTED column and must NOT be listed on INSERT either.
-- Items/BOMs stay per-test (hermetic, unique GUIDs each run - see
-- tests/Erp.Api.IntegrationTests/WorkOrderManufacturingApiTests.cs).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Manufacturing GL leaves (spec MF-02/MF-03) --------------------------------
-- 1320 Work In Progress Stock : the asset account the WIP transit warehouse posts to
--      (MF-02: Dr 1320 / Cr 1310; MF-03: Cr 1320).
-- 1330 Finished Goods Stock   : the asset account the FG warehouse posts to (MF-03: Dr 1330).
-- Both are children of 1000 Assets, mirroring the 1310 Stock In Hand insert style.

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001320')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001320', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '1320', 'Work In Progress Stock', 'Asset', 0, 'a0000000-0000-4000-8000-000000001000', 'USD', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001330')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001330', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '1330', 'Finished Goods Stock', 'Asset', 0, 'a0000000-0000-4000-8000-000000001000', 'USD', 1);
GO

-- --- WIP transit warehouse ------------------------------------------------------
-- Leaf under the WH group root (d000...0001), posting to 1320 - unlike WH-01/WH-02
-- (both on 1310), transfers INTO this warehouse book GL (Dr 1320 / Cr 1310, MF-02).

IF NOT EXISTS (SELECT 1 FROM dbo.Warehouse WHERE Id = 'd0000000-0000-4000-8000-000000000004')
    INSERT INTO dbo.Warehouse (Id, TenantId, CompanyId, WarehouseCode, WarehouseName, ParentWarehouseId, AccountId, IsGroup, IsActive)
    VALUES ('d0000000-0000-4000-8000-000000000004',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'WIP-01', 'WIP Transit',
            'd0000000-0000-4000-8000-000000000001',
            'a0000000-0000-4000-8000-000000001320', 0, 1);
GO

-- --- Workstation WS-01 (spec MF-01: 25 labor + 10 electricity + 5 rent = $40/hour) --

IF NOT EXISTS (SELECT 1 FROM dbo.Workstation WHERE Id = 'e0000000-0000-4000-8000-000000000001')
    INSERT INTO dbo.Workstation (Id, TenantId, CompanyId, WorkstationName, HourRateLabor, HourRateElectricity, HourRateRent, IsActive, CreatedAt)
    VALUES ('e0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'WS-01', 25.0000, 10.0000, 5.0000, 1, SYSDATETIMEOFFSET());
GO

-- Verification
SELECT AccountCode, AccountName, RootType, IsGroup
FROM dbo.Account
WHERE TenantId = '11111111-1111-4111-8111-111111111111' AND AccountCode IN ('1320', '1330')
ORDER BY AccountCode;

SELECT WarehouseCode, WarehouseName, AccountId, IsGroup FROM dbo.Warehouse
WHERE Id = 'd0000000-0000-4000-8000-000000000004';

SELECT WorkstationName, HourRateLabor, HourRateElectricity, HourRateRent, HourRateTotal, IsActive
FROM dbo.Workstation
WHERE Id = 'e0000000-0000-4000-8000-000000000001';
GO
