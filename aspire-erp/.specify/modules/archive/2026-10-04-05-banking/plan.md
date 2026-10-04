# Technical Plan: Banking & Reconciliation (ERPNext Parity)

**Module:** `05-banking`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, Staging Isolation, Automated Heuristic Matching  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Commercial Bank Profile & Account
CREATE TABLE BankAccount (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    AccountName NVARCHAR(100) NOT NULL,
    BankName NVARCHAR(100) NOT NULL,
    AccountNumber NVARCHAR(50) NOT NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    GLAccountId UNIQUEIDENTIFIER NOT NULL,
    LastReconciledBalance DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    LastReconciledDate DATE NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CONSTRAINT FK_BankAccount_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_BankAccount_GLAccount FOREIGN KEY (GLAccountId) REFERENCES Account(Id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.BankAccountHistory));

-- 2. Bank Statement Import Batch
CREATE TABLE BankStatementImport (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    BankAccountId UNIQUEIDENTIFIER NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    ImportDate DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    TotalTransactionsCount INT NOT NULL DEFAULT 0,
    ImportStatus NVARCHAR(30) NOT NULL DEFAULT 'Processed',
    CONSTRAINT FK_BankStatementImport_BankAccount FOREIGN KEY (BankAccountId) REFERENCES BankAccount(Id)
);

-- 3. Bank Transaction (Isolated Staging Entity)
CREATE TABLE BankTransaction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    BankAccountId UNIQUEIDENTIFIER NOT NULL,
    TransactionDate DATE NOT NULL,
    Deposit DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Withdrawal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    Description NVARCHAR(500) NOT NULL,
    ReferenceNumber NVARCHAR(100) NULL,
    TransactionId NVARCHAR(100) NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Unreconciled', -- Unreconciled, Matched, Reconciled, Excluded
    AllocatedAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    ClearanceDate DATE NULL,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_Deposit_NonNegative CHECK (Deposit >= 0),
    CONSTRAINT CK_Withdrawal_NonNegative CHECK (Withdrawal >= 0),
    CONSTRAINT FK_BankTransaction_BankAccount FOREIGN KEY (BankAccountId) REFERENCES BankAccount(Id)
);

CREATE NONCLUSTERED INDEX IX_BankTransaction_Tenant_Account_Status 
ON BankTransaction (TenantId, BankAccountId, Status);

-- 4. Bank Transaction Rule (Heuristic Pattern Engine)
CREATE TABLE BankTransactionRule (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    RuleName NVARCHAR(100) NOT NULL,
    Priority INT NOT NULL DEFAULT 1,
    BankAccountId UNIQUEIDENTIFIER NULL,
    ConditionType NVARCHAR(50) NOT NULL, -- Contains, StartsWith, RegexMatch, AmountEquals
    Pattern NVARCHAR(255) NOT NULL,
    TargetPartyType NVARCHAR(50) NULL,
    TargetPartyId UNIQUEIDENTIFIER NULL,
    AutoCreateVoucher BIT NOT NULL DEFAULT 0,
    TargetExpenseAccountId UNIQUEIDENTIFIER NULL,
    IsActive BIT NOT NULL DEFAULT 1
);
```

---

## 2. Heuristic Matching Engine

```csharp
namespace Erp.Domain.Services;

public sealed class BankRuleMatcher
{
    public static bool Evaluate(BankTransactionRule rule, BankTransaction transaction)
    {
        return rule.ConditionType switch
        {
            RuleConditionType.Contains => transaction.Description.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleConditionType.StartsWith => transaction.Description.StartsWith(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleConditionType.RegexMatch => System.Text.RegularExpressions.Regex.IsMatch(transaction.Description, rule.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            RuleConditionType.AmountEquals => decimal.TryParse(rule.Pattern, out var amt) && Math.Abs(transaction.Deposit - transaction.Withdrawal) == amt,
            _ => false
        };
    }
}
```
