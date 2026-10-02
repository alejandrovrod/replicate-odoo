# Technical Plan: Human Resources & Payroll (ERPNext Parity)

**Module:** `09-hr-payroll`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, Two-Phase Payroll Accrual, Statutory Deductions  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Department Organization Node
CREATE TABLE Department (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DepartmentName NVARCHAR(100) NOT NULL,
    ParentDepartmentId UNIQUEIDENTIFIER NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Department_Parent FOREIGN KEY (ParentDepartmentId) REFERENCES Department(Id)
);

-- 2. Designation Role
CREATE TABLE Designation (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DesignationName NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

-- 3. Employee Master
CREATE TABLE Employee (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    EmployeeNumber NVARCHAR(50) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    WorkEmail NVARCHAR(150) NOT NULL,
    DepartmentId UNIQUEIDENTIFIER NULL,
    DesignationId UNIQUEIDENTIFIER NULL,
    DateOfJoining DATE NOT NULL,
    DateOfRelieving DATE NULL,
    SalaryMode NVARCHAR(20) NOT NULL DEFAULT 'Bank', -- Bank, Cash, Cheque
    BankName NVARCHAR(100) NULL,
    BankAccountNumber NVARCHAR(50) NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Active', -- Active, Inactive, Suspended, Left
    IsActive BIT NOT NULL DEFAULT 1,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CONSTRAINT FK_Employee_Department FOREIGN KEY (DepartmentId) REFERENCES Department(Id),
    CONSTRAINT FK_Employee_Designation FOREIGN KEY (DesignationId) REFERENCES Designation(Id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.EmployeeHistory));

CREATE UNIQUE NONCLUSTERED INDEX UQ_Employee_Tenant_Company_Code 
ON Employee (TenantId, CompanyId, EmployeeNumber);

-- 4. Salary Component (Earnings & Deductions)
CREATE TABLE SalaryComponent (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ComponentName NVARCHAR(100) NOT NULL,
    Type NVARCHAR(20) NOT NULL, -- Earning, Deduction
    DependsOnPaymentDays BIT NOT NULL DEFAULT 0,
    IsTaxApplicable BIT NOT NULL DEFAULT 1,
    DefaultGLAccountId UNIQUEIDENTIFIER NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_SalaryComponent_Account FOREIGN KEY (DefaultGLAccountId) REFERENCES Account(Id)
);

-- 5. Payroll Entry (Monthly Batch Run)
CREATE TABLE PayrollEntry (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    PayrollNumber NVARCHAR(50) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    PostingDate DATE NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Draft', -- Draft, Submitted, Paid, Cancelled
    TotalGrossPay DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TotalDeductions DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TotalNetPay AS (TotalGrossPay - TotalDeductions),
    AccrualJournalEntryId UNIQUEIDENTIFIER NULL,
    PaymentVoucherId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

-- 6. Salary Slip (Pay Stub)
CREATE TABLE SalarySlip (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    PayrollEntryId UNIQUEIDENTIFIER NOT NULL,
    EmployeeId UNIQUEIDENTIFIER NOT NULL,
    SlipNumber NVARCHAR(50) NOT NULL,
    PaymentDays INT NOT NULL DEFAULT 30,
    AbsentDays INT NOT NULL DEFAULT 0,
    GrossPay DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TotalDeductions DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    NetPay AS (GrossPay - TotalDeductions),
    Status NVARCHAR(30) NOT NULL DEFAULT 'Draft',
    CONSTRAINT FK_SalarySlip_PayrollEntry FOREIGN KEY (PayrollEntryId) REFERENCES PayrollEntry(Id) ON DELETE CASCADE,
    CONSTRAINT FK_SalarySlip_Employee FOREIGN KEY (EmployeeId) REFERENCES Employee(Id)
);
```

---

## 2. Payroll Net Pay Calculation Logic

```csharp
namespace Erp.Domain.Services;

public sealed class PayrollCalculator
{
    public static (decimal grossPay, decimal totalDeductions, decimal netPay) CalculateSalarySlip(
        decimal baseEarnings,
        IEnumerable<decimal> allowances,
        IEnumerable<decimal> deductions)
    {
        var gross = baseEarnings + allowances.Sum();
        var totalDed = deductions.Sum();
        var net = Math.Max(0.0m, gross - totalDed);

        return (gross, totalDed, net);
    }
}
```
