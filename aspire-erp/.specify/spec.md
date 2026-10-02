# Functional Specification (PRD): Multi-Tenant Cloud ERP Core (ERPNext Parity)

**Status:** APPROVED  
**Format:** Spec Kit Functional Specification (ERPNext Modular Parity & Gherkin Scenarios)  
**Version:** 2.0.0  
**Business Rules Engine:** [domain_business_rules_ddd.md](./domain_business_rules_ddd.md)  

---

## 1. Executive Summary & Modular Architecture

To achieve true parity with **ERPNext**, this system is organized into **4 Core Operational Modules** interconnected through an immutable double-entry General Ledger and an automated perpetual inventory valuation engine (Kardex).

```mermaid
graph TD
    subgraph Selling Module
        Customer["Customer"] --> Quotation["Quotation (Cotización)"]
        Quotation --> SalesOrder["Sales Order (Pedido de Venta)"]
        SalesOrder --> DeliveryNote["Delivery Note (Remisión/Despacho)"]
        SalesOrder --> SalesInvoice["Sales Invoice (Factura de Venta)"]
    end

    subgraph Buying Module
        Supplier["Supplier"] --> PurchaseOrder["Purchase Order (Orden de Compra)"]
        PurchaseOrder --> PurchaseReceipt["Purchase Receipt (Recepción Almacén)"]
        PurchaseOrder --> PurchaseInvoice["Purchase Invoice (Factura de Proveedor)"]
    end

    subgraph Stock Module
        Item["Item / SKU"] --> Warehouse["Warehouse (Almacén)"]
        DeliveryNote --> StockLedger["Stock Ledger Entry (Kardex FIFO)"]
        PurchaseReceipt --> StockLedger
        StockEntry["Stock Entry (Ajustes/Transferencias)"] --> StockLedger
    end

    subgraph Accounts Module (Core)
        SalesInvoice --> GLEntry["General Ledger (GL Entry)"]
        PurchaseInvoice --> GLEntry
        DeliveryNote -.->|"COGS Posting"| GLEntry
        PurchaseReceipt -.->|"Stock Received But Not Billed"| GLEntry
        PaymentEntry["Payment Entry (Cobro/Pago)"] --> GLEntry
        PaymentEntry --> PaymentAllocation["Reconciliation"]
        PaymentAllocation --> SalesInvoice
        PaymentAllocation --> PurchaseInvoice
    end
```

---

## 2. Module 1: Accounts (Contabilidad & Finanzas)

*ERPNext Parity: `erpnext/accounts/doctype`*

### 2.1 DocTypes & Entities
- **`Account`**: Hierarchical Chart of Accounts (Asset, Liability, Equity, Income, Expense, `parent_account`, `is_group`, `currency`).
- **`FiscalYear` & `PeriodClosingVoucher`**: Accounting periods with hard lock dates and annual retained earnings closing.
- **`JournalEntry`**: Manual multi-line adjustment vouchers enforcing $\sum \text{Debit} = \sum \text{Credit}$.
- **`GLEntry`**: The atomic, immutable transaction ledger.
- **`PaymentEntry`**: Bank and cash receipts/disbursements.
- **`PaymentAllocation`**: Debt extinction against open sales/purchase invoices.
- **`CostCenter` & `AccountingDimension`**: Analytical accounting distribution.

### 2.2 Scenarios & Functional Rules
- **Rule AC-01 (Partida Doble Inviolable):** Every posted transaction must balance debits and credits down to 4 decimal places.
- **Rule AC-02 (Cierre de Periodo Fiscal):** No transaction can be posted, modified, or cancelled if `PostingDate <= Company.PeriodLockDate`.
- **Rule AC-03 (Diferencial Cambiario Automático):** Differences in exchange rates between invoice posting and payment date generate automatic gain/loss entries (`Realized FX Gain/Loss`).
- **Scenario AC-S1 (Gherkin):**
  - **Given** an open invoice for $1,000 USD booked at exchange rate 1.05 ($1,050 Base)
  - **When** the invoice is paid when the exchange rate is 1.10 ($1,100 Base)
  - **Then** the payment extinguishes the $1,000 USD receivable
  - **And** generates a Credit of $50 Base Currency to `4210 - Realized Foreign Exchange Gain`.

---

## 3. Module 2: Stock & Inventory (Inventario & Almacenes)

*ERPNext Parity: `erpnext/stock/doctype`*

### 3.1 DocTypes & Entities
- **`Item` (Artículo/Producto):** SKU, Item Name, Item Group, UOM, Valuation Method (FIFO / Moving Average), Default Income Account, Default Expense Account.
- **`Warehouse` (Almacén/Depósito):** Hierarchical locations (e.g. `Main Warehouse`, `Transit`, `Scrap`), Parent Warehouse, Account link for automated inventory valuation.
- **`UOM` (Unidad de Medida):** Unit conversions (e.g. 1 Box = 12 Units).
- **`StockEntry` (Movimiento de Stock):**
  - Types: *Material Receipt*, *Material Issue*, *Material Transfer between Warehouses*.
- **`StockLedgerEntry` (Kardex Perpetuo):**
  - Immutable historical record of quantity and valuation change per Item and Warehouse.

### 3.2 Scenarios & Functional Rules
- **Rule ST-01 (Perpetual Inventory Valuation):**
  Moving items in or out of a warehouse immediately creates balanced General Ledger entries linking the Inventory Asset Account with Cost of Goods Sold (COGS) or Stock Adjustment accounts.
- **Rule ST-02 (Negative Stock Restriction):**
  If `Company.AllowNegativeStock == false`, an item issue cannot reduce stock quantity below zero in any warehouse.
- **Scenario ST-S1 (Gherkin):**
  - **Given** Warehouse `Main` holds 10 units of Item `LAPTOP-01` valued at $800.00 each
  - **When** a Stock Entry (Material Issue) for 2 units is posted for department expense
  - **Then** `StockLedgerEntry` records $-2$ units at $800.00 ($1,600.00 total valuation decrease)
  - **And** `GLEntry` records:
    - Debit: `5100 - Department Expense` = $1,600.00
    - Credit: `1300 - Stock In Hand (Main Warehouse)` = $1,600.00.

---

## 4. Module 3: Selling (Ciclo Comercial de Ventas)

*ERPNext Parity: `erpnext/selling/doctype`*

### 4.1 DocTypes & Entities
- **`Customer`**: Commercial name, Tax ID, Customer Group, Credit Limit, Payment Terms, Default Currency.
- **`Quotation` (Cotización):** Pre-sales quotation with validity date, items, discounts, and print preview.
- **`SalesOrder` (Pedido de Venta):** Confirmed customer order reserving stock allocation. Statuses: `Draft` -> `To Deliver & Bill` -> `To Bill` -> `Completed`.
- **`DeliveryNote` (Remisión / Guía de Despacho):** Physical fulfillment of goods that triggers stock deduction and COGS accounting entries.
- **`SalesInvoice` (Factura de Venta):** Fiscal invoice triggering Accounts Receivable and Revenue posting.

### 4.2 Workflows & Scenarios
```
[ Quotation ] ──> [ Sales Order ] ──┬──> [ Delivery Note ] (Stock deduction & COGS)
                                    └──> [ Sales Invoice ] (Accounts Receivable & Revenue)
```
- **Rule SE-01 (Three-Way Sales Matching):**
  The system tracks `DeliveredQuantity` vs `BilledQuantity` on each `SalesOrderItem`. A Sales Order is only `Completed` when all items are 100% delivered and 100% billed.
- **Rule SE-02 (Customer Credit Limit Check):**
  If `Customer.EnforceCreditLimit == true`, confirming a Sales Order or posting an invoice is blocked if total outstanding balance exceeds the customer's credit limit.

---

## 5. Module 4: Buying (Ciclo de Compras & Aprovisionamiento)

*ERPNext Parity: `erpnext/buying/doctype`*

### 5.1 DocTypes & Entities
- **`Supplier` (Proveedor):** Name, Tax ID, Supplier Group, Default Payable Account, Payment Terms.
- **`PurchaseOrder` (Orden de Compra):** Binding agreement to purchase goods/services at agreed prices.
- **`PurchaseReceipt` (Recepción de Mercancía):** Physical intake of goods at warehouse. Increases stock and credits `Stock Received But Not Billed` interim account.
- **`PurchaseInvoice` (Factura de Proveedor):** Vendor bill. Debits `Stock Received But Not Billed` and output VAT, and credits Accounts Payable.

### 5.2 Workflows & Scenarios
```
[ Purchase Order ] ──┬──> [ Purchase Receipt ] (Stock increment & Interim Accrual)
                     └──> [ Purchase Invoice ] (Extinguishes Interim Accrual & Books Accounts Payable)
```
- **Rule BU-01 (Accrual Accounting on Goods Receipt):**
  When items arrive before the vendor invoice:
  - **Debit:** `Stock In Hand (Asset)` = $\text{Qty} \times \text{ValuationRate}$
  - **Credit:** `Stock Received But Not Billed (Interim Liability)` = $\text{Qty} \times \text{ValuationRate}$.
  When the vendor invoice arrives:
  - **Debit:** `Stock Received But Not Billed` (clearing the interim liability)
  - **Debit:** `Input VAT / Tax Recoverable`
  - **Credit:** `Accounts Payable (Supplier Liability)`.

---

## 6. Financial Reporting & Executive Analytics Parity

The system provides 6 core financial reports mirroring ERPNext's standard statements:

1. **Balance Sheet (Balance General):** Assets, Liabilities, and Equity evaluated as of a specific date.
2. **Profit and Loss (Estado de Resultados):** Revenues, COGS, Operating Expenses, and Net Profit for a period.
3. **Trial Balance (Balance de Comprobación):** Verifies ledger integrity ($\sum \text{Debits} == \sum \text{Credits}$).
4. **General Ledger (Libro Mayor):** Chronological audit trail per account.
5. **Accounts Receivable Aging (Antigüedad de Saldos Clientes):** Buckets (0-30, 31-60, 61-90, 90+ days).
6. **Stock Ledger / Inventory Valuation (Kardex):** Inflow, outflow, and closing balance per item and warehouse.
