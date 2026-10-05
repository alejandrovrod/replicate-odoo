-- =============================================================================
-- Dev-only seed: HR & Payroll GL leaves (module 09-hr-payroll, task 12.5)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so the frontend .env.development and
-- manual curl sessions can reference fixed ids:
--   Tenant  : 11111111-1111-4111-8111-111111111111 (scripts/seed-dev-coa.sql)
--   Company : 22222222-2222-4222-8222-222222222222 (scripts/seed-dev-coa.sql)
--   Accounts: a0000000-0000-4000-8000-00000000<5130|2220|2225|2150>
--
-- Depends on seed-dev-coa.sql (tenant, company, 2000 Liabilities / 5000 Expenses roots).
-- Account is system-versioned (Constitution Article IV.2): PeriodStart/PeriodEnd
-- are GENERATED ALWAYS columns and must NOT be listed on INSERT.
-- RootType is stored as the enum NAME (AccountConfiguration.HasConversion<string>).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Payroll GL leaves (spec HR-02) ------------------------------------------
-- 5130 Salaries and Wages Expense : the expense debited with gross earnings on accrual.
--      5110 is TAKEN ("Office Supplies Expense" - 01-accounting tests assert it),
--      5120 is TAKEN ("Purchase Price Difference", scripts/seed-dev-buying.sql),
--      5210 is TAKEN ("Cost of Goods Sold"). 5130 is the next free 51xx leaf.
-- 2220 Income Tax Payable         : the liability credited with tax deductions on accrual.
-- 2225 Social Security Payable    : the liability credited with pension deductions on accrual.
-- 2150 Payroll Payable            : the liability credited with TotalNetPay on accrual and
--      debited back to zero on disbursement (spec HR-02 Phase 2). None of 2220/2225/2150
--      exist in seed-dev-coa.sql (verified: only 2110/2120 under 2000 Liabilities).

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000005130')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000005130', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '5130', 'Salaries and Wages Expense', 'Expense', 0, 'a0000000-0000-4000-8000-000000005000', 'USD', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000002220')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000002220', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '2220', 'Income Tax Payable', 'Liability', 0, 'a0000000-0000-4000-8000-000000002000', 'USD', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000002225')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000002225', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '2225', 'Social Security Payable', 'Liability', 0, 'a0000000-0000-4000-8000-000000002000', 'USD', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000002150')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000002150', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '2150', 'Payroll Payable', 'Liability', 0, 'a0000000-0000-4000-8000-000000002000', 'USD', 1);
GO

-- --- Company payroll default (decision D3, same code-not-FK shape as seed-dev-coa.sql) --
-- PayrollPayableAccountCode is the ACCOUNT CODE the accrual credits with TotalNetPay
-- (spec HR-02). Guarded UPDATE: only touches the dev company row, safe to re-run.

IF EXISTS (SELECT 1 FROM dbo.Company WHERE Id = '22222222-2222-4222-8222-222222222222')
    UPDATE dbo.Company
    SET PayrollPayableAccountCode = '2150'
    WHERE Id = '22222222-2222-4222-8222-222222222222'
      AND (PayrollPayableAccountCode IS NULL OR PayrollPayableAccountCode <> '2150');
GO

-- Verification: the four seeded leaves, then the company payroll default.
SELECT AccountCode, AccountName, RootType, IsGroup
FROM dbo.Account
WHERE TenantId = '11111111-1111-4111-8111-111111111111' AND AccountCode IN ('5130', '2220', '2225', '2150')
ORDER BY AccountCode;

SELECT Id, PayrollPayableAccountCode
FROM dbo.Company
WHERE Id = '22222222-2222-4222-8222-222222222222';
GO
