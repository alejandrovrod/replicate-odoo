# Technical Plan: Selling & Point of Sale (ERPNext Parity)

**Module:** `03-selling`  
**Status:** APPROVED  
**Version:** 1.2.0 (Amendment A1 — §1.6 Delivery Note, task 5.2b; Amendment A2 — §1.7 Sales Invoice Line + Company selling GL defaults, task 5.3)  
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

-- 6. Delivery Note (Amendment A1, 2026-10-03, approved — task 5.2b): the physical shipment
-- document of spec SL-01/SL-04. Relieves inventory through the FIFO engine and books COGS at
-- posting time. Stock value comes from the cost layers, NOT from the order rate — hence no money
-- columns here (contrast PurchaseReceiptLine, whose Rate values the receipt-time inventory).
-- Shape mirrors the PurchaseReceipt entity: WarehouseId + VoucherNo (gapless, Constitution III.4).
CREATE TABLE DeliveryNote (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    VoucherNo NVARCHAR(50) NOT NULL,
    SalesOrderId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    PostingDate DATE NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_DeliveryNote_SalesOrder FOREIGN KEY (SalesOrderId) REFERENCES SalesOrder(Id),
    CONSTRAINT FK_DeliveryNote_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
);

CREATE UNIQUE NONCLUSTERED INDEX UQ_DeliveryNote_Tenant_Company_VoucherNo
ON DeliveryNote (TenantId, CompanyId, VoucherNo);

CREATE TABLE DeliveryNoteLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    DeliveryNoteId UNIQUEIDENTIFIER NOT NULL,
    SalesOrderItemId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    Qty DECIMAL(18,4) NOT NULL,
    CONSTRAINT CK_DeliveryNoteLine_Qty CHECK (Qty > 0.0000),
    CONSTRAINT FK_DeliveryNoteLine_Header FOREIGN KEY (DeliveryNoteId) REFERENCES DeliveryNote(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DeliveryNoteLine_OrderLine FOREIGN KEY (SalesOrderItemId) REFERENCES SalesOrderItem(Id),
    CONSTRAINT FK_DeliveryNoteLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- 7. Sales Invoice Line (Amendment A2, 2026-10-03 - announced at the Task 5.3 kickoff): the
-- original plan defined only the SalesInvoice header; NetTotal/TaxTotal/GrandTotal need line
-- detail, and spec SL-01's tax split needs a per-line rate. Tax model (minimal, the spec is
-- silent beyond SL-01/SL-03 amounts): the client PROPOSES TaxRate (percent) per line; the SERVER
-- recomputes TaxAmount and derives every header total authoritatively - the same doctrine as
-- SalesOrder's server-computed totals. Discounts are NOT modelled (spec-silent; revisit with the
-- 5.5 frontend if required).
CREATE TABLE SalesInvoiceLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    SalesInvoiceId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    Qty DECIMAL(18,4) NOT NULL,
    Rate DECIMAL(18,4) NOT NULL,
    Amount DECIMAL(18,4) NOT NULL,
    TaxRate DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    TaxAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    CONSTRAINT CK_SalesInvoiceLine_Qty CHECK (Qty > 0.0000),
    CONSTRAINT CK_SalesInvoiceLine_Rate CHECK (Rate >= 0.0000),
    CONSTRAINT CK_SalesInvoiceLine_Amount CHECK (Amount >= 0.0000),
    CONSTRAINT CK_SalesInvoiceLine_TaxRate CHECK (TaxRate >= 0.0000 AND TaxRate <= 100.00),
    CONSTRAINT CK_SalesInvoiceLine_TaxAmount CHECK (TaxAmount >= 0.0000),
    CONSTRAINT FK_SalesInvoiceLine_Header FOREIGN KEY (SalesInvoiceId) REFERENCES SalesInvoice(Id) ON DELETE CASCADE,
    CONSTRAINT FK_SalesInvoiceLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- Company selling-side GL defaults (Amendment A2): the mirror of the buying trio added by
-- 04-buying Task 4.3 (decision D3 - code, not FK). The Company table itself lives in the core
-- schema, so this module only ALTERs it:
--   ALTER TABLE Company ADD ReceivableAccountCode NVARCHAR(50) NULL;
--     -- Dr on SalesInvoice (spec SL-01 "Accounts Receivable"); per-customer override is
--     -- Customer.DefaultReceivableAccountId (nullable FK, resolves first when set).
--   ALTER TABLE Company ADD SalesRevenueAccountCode NVARCHAR(50) NULL;
--     -- Cr NetTotal (spec SL-01 "Sales Revenue"). No item-level income account exists (the
--     -- Item.IncomeAccountId column was dropped in 02-stock), so the default is company-wide.
--   ALTER TABLE Company ADD OutputTaxPayableAccountCode NVARCHAR(50) NULL;
--     -- Cr TaxTotal (spec SL-01 "Tax Payable"); twin of Company.InputTaxRecoverableAccountCode.
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
