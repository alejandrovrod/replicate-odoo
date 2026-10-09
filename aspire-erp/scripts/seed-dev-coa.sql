-- =============================================================================
-- Dev-only seed: Tenant + Company + Chart of Accounts (plan.md / tasks 2.3-2.4)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so the frontend .env.development and
-- manual curl sessions can reference fixed ids:
--   Tenant  : 11111111-1111-4111-8111-111111111111
--   Company : 22222222-2222-4222-8222-222222222222
--   Accounts: a0000000-0000-4000-8000-00000000<code>
--
-- Account is system-versioned (Constitution Article IV.2): PeriodStart/PeriodEnd
-- are GENERATED ALWAYS columns and must NOT be listed on INSERT.
-- RootType is stored as the enum NAME (AccountConfiguration.HasConversion<string>).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Tenant -----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Tenant WHERE Id = '11111111-1111-4111-8111-111111111111')
BEGIN
    INSERT INTO dbo.Tenant (Id, Name, Code, IsActive, CreatedAt)
    VALUES ('11111111-1111-4111-8111-111111111111', 'Aspire Dev Tenant', 'DEV', 1, SYSDATETIMEOFFSET());
END;
GO

-- --- Company ----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Company WHERE Id = '22222222-2222-4222-8222-222222222222')
BEGIN
    INSERT INTO dbo.Company (Id, TenantId, Name, TaxId, CreatedAt)
    VALUES ('22222222-2222-4222-8222-222222222222',
            '11111111-1111-4111-8111-111111111111',
            'Aspire Dev Company', 'DEV-TAXID', SYSDATETIMEOFFSET());
END;
GO

-- --- Chart of Accounts ------------------------------------------------------
-- Roots (IsGroup = 1): 1000 Assets, 2000 Liabilities, 3000 Equity,
--                      4000 Income, 5000 Expenses
-- Children (IsGroup = 0) inherit their parent's RootType.

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001000')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001000', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '1000', 'Assets', 'Asset', 1, NULL, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001110')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001110', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '1110', 'Cash and Cash Equivalents', 'Asset', 0, 'a0000000-0000-4000-8000-000000001000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001120')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, Type, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001120', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '1120', 'Deudores por Ventas', 'Asset', 'Receivable', 0, 'a0000000-0000-4000-8000-000000001000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000002000')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000002000', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '2000', 'Liabilities', 'Liability', 1, NULL, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000002110')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000002110', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '2110', 'Accounts Payable', 'Liability', 0, 'a0000000-0000-4000-8000-000000002000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000003000')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000003000', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '3000', 'Equity', 'Equity', 1, NULL, 1);

-- 3100 Retained Earnings (spec R-13 / FC-03): the single Equity leaf every
-- PeriodClosingVoucher credits on profit / debits on loss. Parent = 3000.
IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000003100')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000003100', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '3100', 'Retained Earnings', 'Equity', 0, 'a0000000-0000-4000-8000-000000003000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000004000')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000004000', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '4000', 'Income', 'Income', 1, NULL, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000004110')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000004110', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '4110', 'Sales Revenue', 'Income', 0, 'a0000000-0000-4000-8000-000000004000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000005000')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000005000', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '5000', 'Expenses', 'Expense', 1, NULL, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000005110')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000005110', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '5110', 'Office Supplies Expense', 'Expense', 0, 'a0000000-0000-4000-8000-000000005000', 1);
GO

-- --- Stock & Inventory accounts (Phase 3, tasks 3.1-3.2 / spec ST-01, ST-02) --
-- 1310 Stock In Hand  : the asset account every warehouse posts its inventory value to.
-- 2120 Stock Received But Not Billed : the interim liability credited on receipts (ST-01).
-- 5210 Cost of Goods Sold : the expense debited on issues at FIFO cost (ST-02).

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000001310')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000001310', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '1310', 'Stock In Hand', 'Asset', 0, 'a0000000-0000-4000-8000-000000001000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000002120')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000002120', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '2120', 'Stock Received But Not Billed', 'Liability', 0, 'a0000000-0000-4000-8000-000000002000', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = 'a0000000-0000-4000-8000-000000005210')
    INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, IsActive)
    VALUES ('a0000000-0000-4000-8000-000000005210', '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222', '5210', 'Cost of Goods Sold', 'Expense', 0, 'a0000000-0000-4000-8000-000000005000', 1);
GO

-- --- Company posting defaults (decision D3 + module 03/04 consumers) --------
-- Every value is an ACCOUNT CODE, not a FK: a Company -> Account FK would be
-- circular (Account already references Company). The posting engine resolves
-- each code to exactly one active leaf account of the same company.
--   StockReceivedAccountCode      : interim accrual credit (receipts, Task 4.3).
--   CogsAccountCode               : FIFO issue / delivery note debit
--                                    (Option A decision; tasks 3.2/5.2b).
--   DefaultReceivableAccountCode  : A/R debit when the customer carries no
--                                    override (Task 5.3, SubmitSalesInvoice).
--   DefaultIncomeAccountCode      : sales revenue credit when the customer
--                                    carries no override (Task 5.3).
-- AllowNegativeStock stays at its plan.md default (0 = forbidden, Task 3.3).

IF EXISTS (SELECT 1 FROM dbo.Company WHERE Id = '22222222-2222-4222-8222-222222222222')
    UPDATE dbo.Company
    SET StockReceivedAccountCode = '2120',
        CogsAccountCode = '5210',
        DefaultReceivableAccountCode = '1120',
        DefaultIncomeAccountCode = '4110',
        DefaultRetainedEarningsAccountId = 'a0000000-0000-4000-8000-000000003100',
        DefaultRetainedEarningsAccountCode = '3100'
    WHERE Id = '22222222-2222-4222-8222-222222222222'
      AND (StockReceivedAccountCode IS NULL OR StockReceivedAccountCode <> '2120'
        OR CogsAccountCode IS NULL OR CogsAccountCode <> '5210'
        OR DefaultReceivableAccountCode IS NULL OR DefaultReceivableAccountCode <> '1120'
        OR DefaultIncomeAccountCode IS NULL OR DefaultIncomeAccountCode <> '4110'
        OR DefaultRetainedEarningsAccountId IS NULL
        OR DefaultRetainedEarningsAccountCode IS NULL OR DefaultRetainedEarningsAccountCode <> '3100');
GO

-- Verification: 5 roots + 9 children = 14 rows, all sharing one Tenant/Company.
SELECT AccountCode, AccountName, RootType, IsGroup, ParentAccountId
FROM dbo.Account
WHERE TenantId = '11111111-1111-4111-8111-111111111111'
ORDER BY AccountCode;

-- FrozenAccountsDate (was PeriodLockDate, renamed by tasks.md 2.2): NULL = open periods.
SELECT Id, AllowNegativeStock, FrozenAccountsDate, StockReceivedAccountCode,
       CogsAccountCode, DefaultReceivableAccountCode, DefaultIncomeAccountCode
FROM dbo.Company
WHERE Id = '22222222-2222-4222-8222-222222222222';
GO
