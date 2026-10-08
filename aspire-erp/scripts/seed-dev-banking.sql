-- =============================================================================
-- Dev-only seed: Banking & Reconciliation (module 05-banking, task 6.7)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so the frontend .env.development and
-- manual curl sessions can reference fixed ids:
--   Tenant       : 11111111-1111-4111-8111-111111111111 (scripts/seed-dev-coa.sql)
--   Company      : 22222222-2222-4222-8222-222222222222 (scripts/seed-dev-coa.sql)
--   Bank account : b0000000-0000-4000-8000-000000000001 (Main Operating, wired to 1110)
--   Rules        : b0000000-0000-4000-8000-000000000011/12/13
--
-- BankAccount is system-versioned (plan.md §1 DDL literal): PeriodStart/PeriodEnd
-- are GENERATED ALWAYS columns and must NOT be listed on INSERT.
-- ConditionType is stored as the enum NAME (BankTransactionRuleConfiguration).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Bank account -----------------------------------------------------------
-- Wired to 1110 Cash and Cash Equivalents (a0000000-...-000000001110), the
-- liquid-asset GL account the quick-voucher dialog credits for bank fees.
IF NOT EXISTS (SELECT 1 FROM dbo.BankAccount WHERE Id = 'b0000000-0000-4000-8000-000000000001')
BEGIN
    INSERT INTO dbo.BankAccount (Id, TenantId, CompanyId, AccountName, BankName, AccountNumber, GLAccountId, LastReconciledBalance, IsActive)
    VALUES ('b0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'Main Operating Account', 'JPMorgan Chase', '004920',
            'a0000000-0000-4000-8000-000000001110', 0, 1);
END;
GO

-- --- Heuristic rules (task 6.3, scenario BN-02) ------------------------------
-- Rule 1 (global): STRIPE payouts suggest the Customer counterparty.
IF NOT EXISTS (SELECT 1 FROM dbo.BankTransactionRule WHERE Id = 'b0000000-0000-4000-8000-000000000011')
BEGIN
    INSERT INTO dbo.BankTransactionRule (Id, TenantId, CompanyId, RuleName, Priority, BankAccountId, ConditionType, Pattern, TargetPartyType, TargetPartyId, AutoCreateVoucher, TargetExpenseAccountId, IsActive)
    VALUES ('b0000000-0000-4000-8000-000000000011',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'Stripe payouts', 1, NULL, 'Contains', 'STRIPE', 'Customer', NULL, 0, NULL, 1);
END;
GO

-- Rule 2 (account-scoped): bank-fee lines report RequiresVoucherCreation against
-- 5110 Office Supplies Expense; the operator confirms in the dialog (task 6.5 owns posting).
IF NOT EXISTS (SELECT 1 FROM dbo.BankTransactionRule WHERE Id = 'b0000000-0000-4000-8000-000000000012')
BEGIN
    INSERT INTO dbo.BankTransactionRule (Id, TenantId, CompanyId, RuleName, Priority, BankAccountId, ConditionType, Pattern, TargetPartyType, TargetPartyId, AutoCreateVoucher, TargetExpenseAccountId, IsActive)
    VALUES ('b0000000-0000-4000-8000-000000000012',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'Bank fees need voucher', 2, 'b0000000-0000-4000-8000-000000000001', 'Contains', 'BANK FEE', NULL, NULL, 1, 'a0000000-0000-4000-8000-000000005110', 1);
END;
GO

-- Rule 3 (global): AWS charges suggest 5110 without auto-voucher.
IF NOT EXISTS (SELECT 1 FROM dbo.BankTransactionRule WHERE Id = 'b0000000-0000-4000-8000-000000000013')
BEGIN
    INSERT INTO dbo.BankTransactionRule (Id, TenantId, CompanyId, RuleName, Priority, BankAccountId, ConditionType, Pattern, TargetPartyType, TargetPartyId, AutoCreateVoucher, TargetExpenseAccountId, IsActive)
    VALUES ('b0000000-0000-4000-8000-000000000013',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'AWS cloud charges', 10, NULL, 'Contains', 'AWS', NULL, NULL, 0, 'a0000000-0000-4000-8000-000000005110', 1);
END;
GO

-- Verification: the seeded account with its GL wiring, then the seeded rules.
SELECT AccountName, BankName, AccountNumber, GLAccountId, IsActive
FROM dbo.BankAccount
WHERE TenantId = '11111111-1111-4111-8111-111111111111';

SELECT RuleName, Priority, BankAccountId, ConditionType, Pattern, AutoCreateVoucher, IsActive
FROM dbo.BankTransactionRule
WHERE TenantId = '11111111-1111-4111-8111-111111111111'
ORDER BY Priority;
GO
