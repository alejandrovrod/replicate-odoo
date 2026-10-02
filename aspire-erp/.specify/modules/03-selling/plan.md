# Technical Plan: Selling & Point of Sale (ERPNext Parity)

**Module:** `03-selling`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, Credit Management, Atomic POS Transactions  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Customer Entity
CREATE TABLE Customer (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    CustomerCode NVARCHAR(50) NOT NULL,
    CustomerName NVARCHAR(150) NOT NULL,
    TaxId NVARCHAR(50) NOT NULL,
    DefaultReceivableAccountId UNIQUEIDENTIFIER NULL,
    CreditLimit DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    BypassCreditLimitCheck BIT NOT NULL DEFAULT 0,
    BillingCurrency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    PaymentTermsDays INT NOT NULL DEFAULT 30,
    OutstandingAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    IsActive BIT NOT NULL DEFAULT 1,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CONSTRAINT CK_Customer_CreditLimit CHECK (CreditLimit >= 0.0000),
    CONSTRAINT FK_Customer_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_Customer_Account FOREIGN KEY (DefaultReceivableAccountId) REFERENCES Account(Id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.CustomerHistory));

CREATE UNIQUE NONCLUSTERED INDEX UQ_Customer_Tenant_Company_Code 
ON Customer (TenantId, CompanyId, CustomerCode);

-- 2. Sales Order (Customer Commitment)
CREATE TABLE SalesOrder (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    OrderNumber NVARCHAR(50) NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    TransactionDate DATE NOT NULL,
    DeliveryDate DATE NOT NULL,
    Status INT NOT NULL DEFAULT 1, -- Draft, Submitted, PartiallyDelivered, Completed, Cancelled
    NetTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TaxTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    GrandTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    DeliveredPercentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    BilledPercentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_SalesOrder_Totals CHECK (NetTotal >= 0.0000 AND TaxTotal >= 0.0000 AND GrandTotal >= 0.0000),
    CONSTRAINT FK_SalesOrder_Customer FOREIGN KEY (CustomerId) REFERENCES Customer(Id)
);

-- 3. Sales Order Item
CREATE TABLE SalesOrderItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    SalesOrderId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    Quantity DECIMAL(18,4) NOT NULL,
    DeliveredQuantity DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    BilledQuantity DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Rate DECIMAL(18,4) NOT NULL,
    Amount DECIMAL(18,4) NOT NULL,
    CONSTRAINT CK_SalesOrderItem_Quantity CHECK (Quantity > 0.0000),
    CONSTRAINT CK_SalesOrderItem_Rate CHECK (Rate >= 0.0000),
    CONSTRAINT CK_SalesOrderItem_Amount CHECK (Amount >= 0.0000),
    CONSTRAINT FK_SalesOrderItem_Header FOREIGN KEY (SalesOrderId) REFERENCES SalesOrder(Id) ON DELETE CASCADE,
    CONSTRAINT FK_SalesOrderItem_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- 4. Sales Invoice (Commercial Revenue Claim)
CREATE TABLE SalesInvoice (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber NVARCHAR(50) NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    PostingDate DATE NOT NULL,
    DueDate DATE NOT NULL,
    Status INT NOT NULL DEFAULT 1, -- Draft, Unpaid, PartiallyPaid, Paid, Cancelled
    IsPOS BIT NOT NULL DEFAULT 0,
    UpdateStock BIT NOT NULL DEFAULT 0,
    SourceWarehouseId UNIQUEIDENTIFIER NULL,
    NetTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TaxTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    GrandTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    OutstandingAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    PaidAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_SalesInvoice_Totals CHECK (NetTotal >= 0.0000 AND TaxTotal >= 0.0000 AND GrandTotal >= 0.0000),
    CONSTRAINT FK_SalesInvoice_Customer FOREIGN KEY (CustomerId) REFERENCES Customer(Id)
);

-- 5. POS Profile (Retail Counter Session Configuration)
CREATE TABLE POSProfile (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ProfileName NVARCHAR(100) NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    CashAccountId UNIQUEIDENTIFIER NOT NULL,
    CardClearingAccountId UNIQUEIDENTIFIER NOT NULL,
    WriteOffAccountId UNIQUEIDENTIFIER NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_POSProfile_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
);
```

---

## 2. Credit Exposure Evaluation Logic

```csharp
namespace Erp.Domain.Services;

public sealed class CreditControlEvaluator
{
    public static void ValidateCreditExposure(Customer customer, decimal newInvoiceAmount)
    {
        if (customer.BypassCreditLimitCheck || customer.CreditLimit <= 0)
        {
            return;
        }

        var totalExposure = customer.OutstandingAmount + newInvoiceAmount;

        if (totalExposure > customer.CreditLimit)
        {
            throw new CreditLimitExceededException(
                customer.CustomerName,
                customer.CreditLimit,
                customer.OutstandingAmount,
                newInvoiceAmount
            );
        }
    }
}
```
