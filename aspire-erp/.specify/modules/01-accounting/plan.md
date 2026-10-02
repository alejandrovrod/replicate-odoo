# Technical Plan: Accounting & General Ledger (ERPNext Parity)

**Module:** `01-accounting`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, DDD, CQRS, Tenant Isolation  

---

## 1. Clean Architecture Topology & Layers

```
aspire-erp/src/
├── Backend/
│   ├── Erp.Domain/
│   │   ├── Entities/
│   │   │   ├── Account.cs (Chart of Accounts node)
│   │   │   ├── Company.cs (Fiscal entity & freeze date)
│   │   │   ├── GLEntry.cs (Atomic immutable general ledger line)
│   │   │   ├── JournalEntry.cs (Manual voucher aggregate root)
│   │   │   └── FiscalYear.cs
│   │   ├── Enums/ (AccountRootType, AccountType, JournalEntryType, JournalEntryStatus)
│   │   ├── Exceptions/ (DoubleEntryImbalanceException, FiscalPeriodLockedException, InvalidPostingAccountException)
│   │   └── Repositories/ (IAccountRepository, IGLEntryRepository, ICompanyRepository)
│   ├── Erp.Application/
│   │   ├── Features/Accounts/ (CreateAccount, GetAccountTreeQuery)
│   │   ├── Features/GeneralLedger/ (PostJournalEntry, GetTrialBalanceQuery, GetBalanceSheetQuery)
│   │   └── DTOs/ (AccountDto, AccountTreeNodeDto, GLEntryDto, TrialBalanceReportDto)
│   ├── Erp.Infrastructure/
│   │   ├── Data/AppDbContext.cs (EF Core multi-tenant scoped filter)
│   │   ├── Data/Configurations/ (AccountConfiguration, GLEntryConfiguration, CompanyConfiguration)
│   │   └── Data/Repositories/ (AccountRepository, GLEntryRepository, CompanyRepository)
│   └── Erp.Api/
│       └── Controllers/V1/ (AccountsController.cs, JournalEntriesController.cs, FinancialReportsController.cs)
└── Frontend/
    └── erp-client/src/
        ├── features/accounting/
        │   ├── AccountTreeTable.tsx (Interactive COA tree table)
        │   ├── GeneralLedgerOverview.tsx (Immutable ledger audit viewer)
        │   └── JournalEntryForm.tsx (Debit/Credit multi-line entry)
        └── store/useTenantStore.ts
```

---

## 2. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Legal Company & Fiscal Lock Boundary
CREATE TABLE Company (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyName NVARCHAR(150) NOT NULL,
    TaxId NVARCHAR(50) NOT NULL,
    DefaultCurrency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    FrozenAccountsDate DATE NULL, -- Accounts Settings freeze_date
    DefaultReceivableAccountId UNIQUEIDENTIFIER NULL,
    DefaultPayableAccountId UNIQUEIDENTIFIER NULL,
    DefaultIncomeAccountId UNIQUEIDENTIFIER NULL,
    DefaultExpenseAccountId UNIQUEIDENTIFIER NULL,
    CostOfGoodsSoldAccountId UNIQUEIDENTIFIER NULL,
    RoundOffAccountId UNIQUEIDENTIFIER NULL,
    RealizedExchangeGainLossAccountId UNIQUEIDENTIFIER NULL,
    RetainedEarningsAccountId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE NONCLUSTERED INDEX IX_Company_Tenant ON Company (TenantId);

-- 2. Chart of Accounts (Temporal Table for Configuration Auditing)
CREATE TABLE Account (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    AccountCode NVARCHAR(50) NOT NULL,
    AccountName NVARCHAR(150) NOT NULL,
    RootType NVARCHAR(20) NOT NULL, -- Asset, Liability, Equity, Income, Expense
    Type NVARCHAR(50) NOT NULL,     -- Bank, Cash, Receivable, Payable, COGS, Stock, etc.
    IsGroup BIT NOT NULL DEFAULT 0,
    ParentAccountId UNIQUEIDENTIFIER NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    IsActive BIT NOT NULL DEFAULT 1,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CONSTRAINT FK_Account_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_Account_Parent FOREIGN KEY (ParentAccountId) REFERENCES Account(Id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.AccountHistory));

CREATE UNIQUE NONCLUSTERED INDEX UQ_Account_Tenant_Company_Code 
ON Account (TenantId, CompanyId, AccountCode);

-- 3. General Ledger (Immutable Append-Only Audit Ledger)
CREATE TABLE GLEntry (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    PostingDate DATE NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Debit DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Credit DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    DebitInAccountCurrency DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    CreditInAccountCurrency DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    AccountCurrency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    VoucherType NVARCHAR(50) NOT NULL, -- Journal Entry, Sales Invoice, Purchase Invoice, Stock Entry
    VoucherNo NVARCHAR(100) NOT NULL,
    VoucherId UNIQUEIDENTIFIER NOT NULL,
    PartyType NVARCHAR(50) NULL,
    PartyId UNIQUEIDENTIFIER NULL,
    CostCenterId UNIQUEIDENTIFIER NULL,
    IsCancelled BIT NOT NULL DEFAULT 0,
    Remarks NVARCHAR(MAX) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_Debit_NonNegative CHECK (Debit >= 0),
    CONSTRAINT CK_Credit_NonNegative CHECK (Credit >= 0),
    CONSTRAINT FK_GLEntry_Account FOREIGN KEY (AccountId) REFERENCES Account(Id)
);

CREATE NONCLUSTERED INDEX IX_GLEntry_Tenant_Company_Account_Date 
ON GLEntry (TenantId, CompanyId, AccountId, PostingDate)
INCLUDE (Debit, Credit, VoucherType, VoucherNo);

CREATE NONCLUSTERED INDEX IX_GLEntry_Tenant_Voucher 
ON GLEntry (TenantId, VoucherType, VoucherId);
```

---

## 3. CQRS Contracts & Commands

### `SubmitJournalEntryCommand`
```csharp
namespace Erp.Application.Features.GeneralLedger.Commands;

public sealed record SubmitJournalEntryCommand(
    Guid CompanyId,
    DateOnly PostingDate,
    JournalEntryType VoucherType,
    string UserRemark,
    IReadOnlyList<JournalEntryLineDto> Lines
) : ICommand<Result<Guid>>;

public sealed record JournalEntryLineDto(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? PartyType,
    Guid? PartyId,
    Guid? CostCenterId
);
```

### Double-Entry Invariant Handler Logic:
```csharp
var totalDebit = request.Lines.Sum(l => l.Debit);
var totalCredit = request.Lines.Sum(l => l.Credit);

if (Math.Abs(totalDebit - totalCredit) > 0.0001m)
{
    throw new DoubleEntryImbalanceException(totalDebit, totalCredit);
}

var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
if (company.FrozenAccountsDate.HasValue && request.PostingDate <= company.FrozenAccountsDate.Value)
{
    throw new FiscalPeriodLockedException(request.PostingDate, company.FrozenAccountsDate.Value);
}

// Ensure no line posts to a group account
foreach (var line in request.Lines)
{
    var account = await _accountRepository.GetByIdAsync(line.AccountId, cancellationToken);
    if (account.IsGroup)
    {
        throw new InvalidPostingAccountException(account.AccountCode, account.AccountName);
    }
}
```

---

## 4. Financial Reporting Projections

- **Trial Balance Query:**
  ```sql
  SELECT a.AccountCode, a.AccountName, a.RootType,
         SUM(gl.Debit) AS TotalDebit,
         SUM(gl.Credit) AS TotalCredit,
         (SUM(gl.Debit) - SUM(gl.Credit)) AS NetBalance
  FROM GLEntry gl
  JOIN Account a ON gl.AccountId = a.Id
  WHERE gl.TenantId = @TenantId AND gl.CompanyId = @CompanyId AND gl.PostingDate <= @AsOfDate
  GROUP BY a.AccountCode, a.AccountName, a.RootType;
  ```
- Invariant check: $\sum \text{TotalDebit} - \sum \text{TotalCredit} == 0.0000$.
