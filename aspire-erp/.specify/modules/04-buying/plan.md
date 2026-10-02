# Technical Plan: Buying & Procurement (ERPNext Parity)

**Module:** `04-buying`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, 3-Way Matching, Interim Liability Accrual  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Supplier Entity
CREATE TABLE Supplier (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    SupplierCode NVARCHAR(50) NOT NULL,
    SupplierName NVARCHAR(150) NOT NULL,
    TaxId NVARCHAR(50) NOT NULL,
    DefaultPayableAccountId UNIQUEIDENTIFIER NULL,
    BillingCurrency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    PaymentTermsDays INT NOT NULL DEFAULT 30,
    OutstandingAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    IsActive BIT NOT NULL DEFAULT 1,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CONSTRAINT FK_Supplier_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_Supplier_Account FOREIGN KEY (DefaultPayableAccountId) REFERENCES Account(Id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.SupplierHistory));

CREATE UNIQUE NONCLUSTERED INDEX UQ_Supplier_Tenant_Company_Code 
ON Supplier (TenantId, CompanyId, SupplierCode);

-- 2. Purchase Order
CREATE TABLE PurchaseOrder (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    OrderNumber NVARCHAR(50) NOT NULL,
    SupplierId UNIQUEIDENTIFIER NOT NULL,
    TransactionDate DATE NOT NULL,
    ScheduleDate DATE NOT NULL,
    Status INT NOT NULL DEFAULT 1, -- Draft, Submitted, PartiallyReceived, Completed, Cancelled
    NetTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TaxTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    GrandTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    ReceivedPercentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    BilledPercentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_PurchaseOrder_Supplier FOREIGN KEY (SupplierId) REFERENCES Supplier(Id)
);

-- 3. Purchase Order Item
CREATE TABLE PurchaseOrderItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    PurchaseOrderId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    Quantity DECIMAL(18,4) NOT NULL,
    ReceivedQuantity DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    BilledQuantity DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Rate DECIMAL(18,4) NOT NULL,
    Amount DECIMAL(18,4) NOT NULL,
    CONSTRAINT FK_PurchaseOrderItem_Header FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrder(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PurchaseOrderItem_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- 4. Purchase Receipt (Goods Intake Header)
CREATE TABLE PurchaseReceipt (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ReceiptNumber NVARCHAR(50) NOT NULL,
    SupplierId UNIQUEIDENTIFIER NOT NULL,
    PostingDate DATE NOT NULL,
    Status INT NOT NULL DEFAULT 1, -- Draft, Submitted, Cancelled
    TotalAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_PurchaseReceipt_Supplier FOREIGN KEY (SupplierId) REFERENCES Supplier(Id)
);

-- 5. Purchase Invoice (Vendor Bill)
CREATE TABLE PurchaseInvoice (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber NVARCHAR(50) NOT NULL,
    BillNumber NVARCHAR(100) NOT NULL, -- Vendor external reference
    SupplierId UNIQUEIDENTIFIER NOT NULL,
    PostingDate DATE NOT NULL,
    DueDate DATE NOT NULL,
    Status INT NOT NULL DEFAULT 1, -- Draft, Unpaid, PartiallyPaid, Paid, Cancelled
    NetTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    TaxTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    WithholdingTaxTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    GrandTotal DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    OutstandingAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_PurchaseInvoice_Supplier FOREIGN KEY (SupplierId) REFERENCES Supplier(Id)
);
```

---

## 2. 3-Way Matching Validation Logic

```csharp
namespace Erp.Domain.Services;

public sealed class ThreeWayMatchValidator
{
    public static void ValidateBillingQuantity(decimal receivedQty, decimal previouslyBilledQty, decimal attemptingToBillQty)
    {
        var allowableQty = receivedQty - previouslyBilledQty;

        if (attemptingToBillQty > allowableQty)
        {
            throw new OverbillingNotAllowedException(
                $"Cannot bill {attemptingToBillQty} units. Maximum remaining billable quantity is {allowableQty}."
            );
        }
    }
}
```
