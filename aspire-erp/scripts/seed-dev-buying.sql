-- =============================================================================
-- Dev-only seed: Buying Cycle accounts + company defaults + demo supplier
-- (Phase 4, tasks 4.1-4.3 / spec BY-01)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so curl sessions and the e2e run can
-- reference fixed ids:
--   Supplier : e0000000-0000-4000-8000-00000000<nnnn>
--   Accounts : a0000000-0000-4000-8000-00000000<code> (same scheme as the COA)
--
-- New accounts (Phase 4):
--   1130 Input Tax Recoverable : the asset debited for PurchaseInvoice.TaxAmount
--                                (spec BY-01: Dr Input Tax Recoverable $100.00).
--   5120 Purchase Price Difference : absorbs the variance when the billed rate
--                                differs from the received rate (Task 4.3).
-- Existing accounts reused:
--   2110 Accounts Payable (Cr gross), 2120 Stock Received But Not Billed (the
--   interim liability the invoice clears), 1310 Stock In Hand (receipt Dr).
--
-- Company defaults follow decision D3 (ACCOUNT CODES, not FKs - a Company ->
-- Account FK would be circular): the posting engine resolves each code to
-- exactly one active leaf account of the same company.
--
-- Depends on seed-dev-coa.sql + seed-dev-stock.sql (tenant, company, items,
-- warehouses, 1310/2110/2120 accounts).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Buying accounts (Phase 4, tasks 4.3 / spec BY-01) ----------------------
-- 1130 Input Tax Recoverable : Asset - the recoupable input tax the vendor bills.
IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001130')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001130', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '1130', 'Input Tax Recoverable', 'Asset', 0, 'a0000000-0000-4000-8000-000000001000', 1);

-- 5120 Purchase Price Difference : Expense - variances between received and billed rates.
IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000005120')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000005120', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '5120', 'Purchase Price Difference', 'Expense', 0, 'a0000000-0000-4000-8000-000000005000', 1);
GO

-- --- Company buying posting defaults (decision D3, Task 4.3) ----------------
-- AccountsPayableAccountCode          : Cr gross payable on vendor bills (BY-01: 2110).
-- InputTaxRecoverableAccountCode      : Dr PurchaseInvoice.TaxAmount (BY-01: 1130).
-- PriceDifferenceAccountCode          : Dr/Cr only when billed rate <> received rate (5120).
IF EXISTS (SELECT 1 FROM dbo.Company WHERE Id = '22222222-2222-4222-8222-222222222222')
BEGIN
    UPDATE dbo.Company
    SET AccountsPayableAccountCode = '2110'
    WHERE Id = '22222222-2222-4222-8222-222222222222'
      AND (AccountsPayableAccountCode IS NULL OR AccountsPayableAccountCode <> '2110');

    UPDATE dbo.Company
    SET InputTaxRecoverableAccountCode = '1130'
    WHERE Id = '22222222-2222-4222-8222-222222222222'
      AND (InputTaxRecoverableAccountCode IS NULL OR InputTaxRecoverableAccountCode <> '1130');

    UPDATE dbo.Company
    SET PriceDifferenceAccountCode = '5120'
    WHERE Id = '22222222-2222-4222-8222-222222222222'
      AND (PriceDifferenceAccountCode IS NULL OR PriceDifferenceAccountCode <> '5120');
END;
GO

-- --- Demo supplier (Task 4.1) ------------------------------------------------
-- Codes are unique per TENANT (IX_Supplier_Tenant_Code); e2e also creates
-- suppliers through the API, this one gives the UI an immediate picker entry.
IF NOT EXISTS (SELECT 1 FROM dbo.Supplier WHERE Id = 'e0000000-0000-4000-8000-000000000001')
    INSERT INTO dbo.Supplier (Id, TenantId, Code, Name, IsActive)
    VALUES ('e0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            'SUP-001', 'Acme Industrial Supplies', 1);
GO

-- Verification
SELECT AccountCode, AccountName, RootType, IsGroup FROM dbo.Account
WHERE AccountCode IN ('1130', '5120', '2110', '2120', '1310')
ORDER BY AccountCode;

SELECT AccountsPayableAccountCode, InputTaxRecoverableAccountCode, PriceDifferenceAccountCode, StockReceivedAccountCode
FROM dbo.Company
WHERE Id = '22222222-2222-4222-8222-222222222222';

SELECT Code, Name, IsActive FROM dbo.Supplier ORDER BY Code;
GO
