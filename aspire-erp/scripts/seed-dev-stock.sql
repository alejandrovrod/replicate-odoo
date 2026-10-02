-- =============================================================================
-- Dev-only seed: UOM + Item + Warehouses (Phase 3, tasks 3.1-3.3)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so curl sessions and the React Task 3.4
-- UI can reference fixed ids:
--   UOM       : b0000000-0000-4000-8000-00000000<nnnn>
--   Item      : c0000000-0000-4000-8000-00000000<nnnn>
--   Warehouse : d0000000-0000-4000-8000-00000000<nnnn>
--
-- Depends on seed-dev-coa.sql (tenant, company, accounts 1310/2120/5210).
-- ValuationMethod is stored as the enum NAME (nvarchar(20)), matching RootType.
-- Item / Warehouse / UOM are NOT system-versioned (Constitution IV.2 lists
-- Account and Company only).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Units of measure -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.UOM WHERE Id = 'b0000000-0000-4000-8000-000000000001')
    INSERT INTO dbo.UOM (Id, TenantId, Code, Name, ToBaseFactor)
    VALUES ('b0000000-0000-4000-8000-000000000001', '11111111-1111-4111-8111-111111111111', 'EA', 'Each', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.UOM WHERE Id = 'b0000000-0000-4000-8000-000000000002')
    INSERT INTO dbo.UOM (Id, TenantId, Code, Name, ToBaseFactor)
    VALUES ('b0000000-0000-4000-8000-000000000002', '11111111-1111-4111-8111-111111111111', 'KG', 'Kilogram', 1);
GO

-- --- Item (FIFO - the only valuation method implemented in Phase 3) ---------
-- ExpenseAccount 5210 (Cost of Goods Sold) is what issues debit at FIFO cost (ST-02).
IF NOT EXISTS (SELECT 1 FROM dbo.Item WHERE Id = 'c0000000-0000-4000-8000-000000000001')
    INSERT INTO dbo.Item (Id, TenantId, Code, Name, ValuationMethod, BaseUOMId, IncomeAccountId, ExpenseAccountId, IsActive)
    VALUES ('c0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            'IT-001', 'Steel Bracket', 'Fifo',
            'b0000000-0000-4000-8000-000000000001',
            'a0000000-0000-4000-8000-000000004110',
            'a0000000-0000-4000-8000-000000005210',
            1);

-- IT-002 / IT-003 exist so the spec §4 scenarios ST-01 and ST-02 can be reproduced with their
-- LITERAL amounts (ST-01: 100 @ $10.00 = $1,000.00; ST-02: layers 50 @ $10 + 50 @ $12).
IF NOT EXISTS (SELECT 1 FROM dbo.Item WHERE Id = 'c0000000-0000-4000-8000-000000000002')
    INSERT INTO dbo.Item (Id, TenantId, Code, Name, ValuationMethod, BaseUOMId, IncomeAccountId, ExpenseAccountId, IsActive)
    VALUES ('c0000000-0000-4000-8000-000000000002',
            '11111111-1111-4111-8111-111111111111',
            'IT-002', 'Widget-A', 'Fifo',
            'b0000000-0000-4000-8000-000000000001',
            'a0000000-0000-4000-8000-000000004110',
            'a0000000-0000-4000-8000-000000005210',
            1);

IF NOT EXISTS (SELECT 1 FROM dbo.Item WHERE Id = 'c0000000-0000-4000-8000-000000000003')
    INSERT INTO dbo.Item (Id, TenantId, Code, Name, ValuationMethod, BaseUOMId, IncomeAccountId, ExpenseAccountId, IsActive)
    VALUES ('c0000000-0000-4000-8000-000000000003',
            '11111111-1111-4111-8111-111111111111',
            'IT-003', 'Widget-B', 'Fifo',
            'b0000000-0000-4000-8000-000000000001',
            'a0000000-0000-4000-8000-000000004110',
            'a0000000-0000-4000-8000-000000005210',
            1);
GO

-- --- Warehouses -------------------------------------------------------------
-- Two leaf warehouses under a Group root; both post their inventory value to
-- 1310 Stock In Hand (the same GL account, so transfers between them preserve
-- value WITHOUT writing GL lines - decision D4).
IF NOT EXISTS (SELECT 1 FROM dbo.Warehouse WHERE Id = 'd0000000-0000-4000-8000-000000000001')
    INSERT INTO dbo.Warehouse (Id, TenantId, CompanyId, Code, Name, ParentWarehouseId, StockAccountId, IsGroup, IsActive)
    VALUES ('d0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'WH', 'All Warehouses', NULL,
            'a0000000-0000-4000-8000-000000001310', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Warehouse WHERE Id = 'd0000000-0000-4000-8000-000000000002')
    INSERT INTO dbo.Warehouse (Id, TenantId, CompanyId, Code, Name, ParentWarehouseId, StockAccountId, IsGroup, IsActive)
    VALUES ('d0000000-0000-4000-8000-000000000002',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'WH-01', 'Main Stores',
            'd0000000-0000-4000-8000-000000000001',
            'a0000000-0000-4000-8000-000000001310', 0, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Warehouse WHERE Id = 'd0000000-0000-4000-8000-000000000003')
    INSERT INTO dbo.Warehouse (Id, TenantId, CompanyId, Code, Name, ParentWarehouseId, StockAccountId, IsGroup, IsActive)
    VALUES ('d0000000-0000-4000-8000-000000000003',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'WH-02', 'Branch Stores',
            'd0000000-0000-4000-8000-000000000001',
            'a0000000-0000-4000-8000-000000001310', 0, 1);
GO

-- Verification
SELECT Code, Name, ToBaseFactor FROM dbo.UOM ORDER BY Code;
SELECT Code, Name, ValuationMethod, BaseUOMId, ExpenseAccountId FROM dbo.Item ORDER BY Code;
SELECT Code, Name, ParentWarehouseId, StockAccountId, IsGroup FROM dbo.Warehouse ORDER BY Code;
GO
