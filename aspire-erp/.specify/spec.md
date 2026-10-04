# Functional Specification (PRD): Multi-Tenant Cloud ERP Core (ERPNext Parity)

**Status:** APPROVED  
**Format:** Spec Kit Functional Specification (ERPNext Modular Parity & Gherkin Scenarios)  
**Version:** 2.1.0  
**Business Rules Engine:** [domain_business_rules_ddd.md](./domain_business_rules_ddd.md)  
**Reference Diagram:** GitDiagram ERPNext Architecture (`group_bank_ui`, `group_bank_domain`, `group_accounting`, `group_operations`)  
**Canonical Docs:** [ERPNext Official Docs](https://docs.frappe.io/erpnext)  

### Modular Specifications (GitHub Spec Kit Triad: Spec · Plan · Tasks):
| Module | Certification | Functional Spec | Technical Plan | Tasks Roadmap |
| :--- | :---: | :--- | :--- | :--- |
| **01. Accounting & General Ledger** | `CERTIFIED — amended scope` | [spec.md](./modules/archive/2026-10-04-01-accounting/spec.md) | [plan.md](./modules/archive/2026-10-04-01-accounting/plan.md) | [tasks.md](./modules/archive/2026-10-04-01-accounting/tasks.md) |
| **02. Stock & Inventory (Kardex FIFO)** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-04-02-stock/spec.md) | [plan.md](./modules/archive/2026-10-04-02-stock/plan.md) | [tasks.md](./modules/archive/2026-10-04-02-stock/tasks.md) |
| **03. Selling & Point of Sale (POS)** | `CERTIFIED — amended scope` | [spec.md](./modules/archive/2026-10-04-03-selling/spec.md) | [plan.md](./modules/archive/2026-10-04-03-selling/plan.md) | [tasks.md](./modules/archive/2026-10-04-03-selling/tasks.md) |
| **04. Buying & Procurement** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-03-04-buying/spec.md) | [plan.md](./modules/archive/2026-10-03-04-buying/plan.md) | [tasks.md](./modules/archive/2026-10-03-04-buying/tasks.md) |
| **05. Banking & Reconciliation Subsystem** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-04-05-banking/spec.md) | [plan.md](./modules/archive/2026-10-04-05-banking/plan.md) | [tasks.md](./modules/archive/2026-10-04-05-banking/tasks.md) |
| **06. Manufacturing & Production (BOM)** | `100% CERTIFIED` | [spec.md](./modules/06-manufacturing/spec.md) | [plan.md](./modules/06-manufacturing/plan.md) | [tasks.md](./modules/06-manufacturing/tasks.md) |
| **07. Asset Management & Depreciation** | `100% CERTIFIED` | [spec.md](./modules/07-assets/spec.md) | [plan.md](./modules/07-assets/plan.md) | [tasks.md](./modules/07-assets/tasks.md) |
| **08. CRM & Sales Pipeline** | `100% CERTIFIED` | [spec.md](./modules/08-crm/spec.md) | [plan.md](./modules/08-crm/plan.md) | [tasks.md](./modules/08-crm/tasks.md) |
| **09. Human Resources & Payroll** | `100% CERTIFIED` | [spec.md](./modules/09-hr-payroll/spec.md) | [plan.md](./modules/09-hr-payroll/plan.md) | [tasks.md](./modules/09-hr-payroll/tasks.md) |

---

## 1. Executive Summary & Modular Architecture

To achieve direct architectural parity with **ERPNext**, this system is organized into **5 Core Operational Modules** interconnected through an immutable double-entry General Ledger, an automated banking reconciliation tool, and a perpetual inventory engine.

```mermaid
graph TD
    subgraph Banking Subsystem (React SPA)
        BankApp["Banking App (App.tsx)"] --> RecPage["Reconciliation Page (BankReconciliation.tsx)"]
        BankApp --> ImportPage["Statement Import (BankStatementImporter.tsx)"]
        ImportPage --> BankStatementImport["Bank Statement Import"]
        BankStatementImport --> BankTx["Bank Transactions (Staging)"]
        BankTx --> BankRules["Transaction Rules Engine"]
        BankRules --> RecTool["Bank Reconciliation Tool"]
        RecTool --> DialogManager["Voucher Dialog (Quick Create)"]
    end

    subgraph Accounting Core
        RecTool --> AccountsController["Accounts Controller"]
        AccountsController --> GLEntry["General Ledger (gl_entry.py)"]
        FinancialReports["Financial Reports"] --> GLEntry
        BankAccountRecords["Bank Account Records"] --> BankTx
    end

    subgraph Business Operations
        POS["Point of Sale (pos_controller.js)"] --> AccountsController
        Buying["Purchasing (buying_controller.py)"] --> AccountsController
        Selling["Sales Invoicing"] --> AccountsController
        Manufacturing["Manufacturing Scheduling (engine.py)"]
    end
```

---

## 2. Module 1: Banking Operations & Dual-Sided Reconciliation

*ERPNext Parity: `banking/src/App.tsx`, `erpnext/accounts/doctype/bank_transaction`, `bank_reconciliation_tool`*

### 2.1 User Journeys & Scenarios

#### Scenario BN-01: Bank Statement Import & Staging
- **Given** an authorized user on the Banking Interface (`BankStatementImporter.tsx`)
- **When** the user uploads a bank statement file (CSV / OFX) containing 50 transactions
- **Then** the file is parsed and stored in `BankStatementImport` with an `ImportLog`
- **And** 50 isolated records are inserted into `BankTransaction` in `Unreconciled` status
- **And** **zero** accounting entries are posted to `GLEntry` (Staging isolation rule).

#### Scenario BN-02: Automated Rule Matching (`BankTransactionRule`)
- **Given** an unreconciled Bank Transaction with description `"STRIPE PAYOUT REF #98234"` of $5,400.00
- **And** an active `BankTransactionRule` matching keyword `"STRIPE PAYOUT"` linked to Customer `Stripe Inc.`
- **When** the reconciliation engine evaluates the rule
- **Then** the transaction status becomes `Matched`
- **And** the customer and default fee accounts are auto-populated in the UI.

#### Scenario BN-03: Dual-Sided Reconciliation & Ledger Confirmation
- **Given** an unreconciled Bank Transaction of $1,000.00 (Deposit)
- **And** an existing open Payment Entry `PAY-2026-0001` of $1,000.00
- **When** the user confirms the match in `BankReconciliation.tsx`
- **Then** `BankTransaction.Status` transitions to `Reconciled`
- **And** the Bank Account's reconciled clearance date is updated
- **And** the difference on the Bank Reconciliation Statement decreases to $0.00.

#### Scenario BN-04: On-the-fly Voucher Creation (`DialogManager`)
- **Given** an unreconciled bank fee line of $15.00 for which no internal voucher exists
- **When** the accountant opens the Voucher Dialog directly from the reconciliation row
- **Then** a `JournalEntry` is created: Debit `5150 - Bank Charges` ($15.00), Credit `1110 - Bank Account` ($15.00)
- **And** the bank transaction is immediately reconciled against the newly generated voucher in one atomic action.

---

## 3. Module 2: Accounting Core (Contabilidad & Finanzas)

*ERPNext Parity: `erpnext/accounts/doctype` & `accounts_controller.py`*

- **`GLEntry`**: The atomic, immutable transaction ledger.
- **`Account`**: Hierarchical Chart of Accounts (Asset, Liability, Equity, Income, Expense).
- **`FiscalYear` & `PeriodClosingVoucher`**: Hard period locks and fiscal closing.
- **`FinancialReports`**: Balance Sheet, Profit & Loss, Trial Balance, Bank Reconciliation Statement.

#### Scenario AC-01: Double-Entry Balance Invariant Enforcement
- **Given** an incoming Journal Entry or Transaction Voucher with debits totaling $1,250.00
- **And** credits totaling $1,245.00 (discrepancy of $5.00)
- **When** the posting pipeline validates the transaction
- **Then** the operation is rejected with `DomainValidationException("DoubleEntryImbalance")`
- **And** zero records are written to `GLEntry`.

#### Scenario AC-02: Hard Fiscal Period Lock
- **Given** a tenant fiscal period closed up to `2025-12-31`
- **When** an accountant attempts to post an invoice or adjustment with `PostingDate = 2025-11-15`
- **Then** the command fails with `FiscalPeriodLockedException`
- **And** the ledger state remains completely unaltered.

#### Scenario AC-03: Multi-Currency Realized FX Gain/Loss
- **Given** an open Sales Invoice of €1,000.00 booked at exchange rate 1.05 (USD $1,050.00 A/R)
- **When** a payment of €1,000.00 is received when the exchange rate is 1.10 (USD $1,100.00 Bank Inflow)
- **Then** the payment debits Bank for $1,100.00, credits Accounts Receivable for $1,050.00
- **And** credits `Realized Exchange Gain` account for $50.00.

---

## 4. Module 3: Stock & Inventory (Inventario & Kardex FIFO)

*ERPNext Parity: `erpnext/stock/doctype` & perpetual inventory engine*

- **`Item` (SKU)**, **`Warehouse` (Hierarchical)**, **`UOM`**.
- **`StockEntry`**: Material Receipt, Issue, and Warehouse Transfer.
- **`StockLedgerEntry`**: Perpetual inventory valuation (Kardex FIFO) generating automated COGS and stock asset ledger movements.

#### Scenario ST-01: Perpetual Inventory Receipt & Valuation
- **Given** a purchase receipt of 100 units of `Widget-A` at $10.00/unit
- **When** the stock receipt is submitted into warehouse `Stores - North`
- **Then** a `StockLedgerEntry` is created for +100 units valued at $1,000.00
- **And** a balanced `GLEntry` is booked: Debit `1310 - Stock In Hand` ($1,000.00), Credit `2120 - Stock Received But Not Billed` ($1,000.00).

#### Scenario ST-02: FIFO Costing Layer Consumption on Delivery Note
- **Given** inventory layers for `Widget-A`: 50 units @ $10.00, followed by 50 units @ $12.00
- **When** a `DeliveryNote` fulfills a sales order for 60 units
- **Then** the FIFO engine consumes 50 units @ $10.00 ($500.00) + 10 units @ $12.00 ($120.00) = $620.00 total COGS
- **And** the GL posting debits `Cost of Goods Sold` ($620.00) and credits `Stock In Hand` ($620.00).

---

## 5. Module 4: Selling & Point of Sale (POS)

*ERPNext Parity: `erpnext/selling/doctype` & `pos_controller.js`*

- **Selling Cycle:** `Customer` $\to$ `Quotation` $\to$ `SalesOrder` $\to$ `DeliveryNote` $\to$ `SalesInvoice`.
- **Point of Sale (POS):** Offline-first / high-speed cashier checkout interface.

#### Scenario SL-01: Customer Credit Limit Breach Protection
- **Given** a Customer with Credit Limit $5,000.00 and current outstanding debt of $4,800.00
- **When** a sales operator attempts to submit a new `SalesInvoice` for $450.00 on credit terms
- **Then** the total exposure ($5,250.00) exceeds the allowed limit
- **And** the posting is blocked with `CreditLimitExceededException` unless approved by a Credit Manager override.

#### Scenario POS-01: High-Speed POS Checkout & Atomic Settlement
- **Given** an active retail POS Cashier Session
- **When** the cashier scans 3 items totaling $45.00 and accepts cash payment of $50.00
- **Then** a `SalesInvoice` is created with `IsPOS = true`
- **And** an atomic `GLEntry` debits `Cash In Drawer` ($45.00), credits `Sales Revenue` ($40.00), credits `Tax Payable` ($5.00)
- **And** stock is immediately relieved from the retail store warehouse
- **And** the POS UI displays change due of $5.00 with receipt printing triggered.

---

## 6. Module 5: Buying & Procurement

*ERPNext Parity: `erpnext/buying/doctype` & `buying_controller.py`*

- **Buying Cycle:** `Supplier` $\to$ `PurchaseOrder` $\to$ `PurchaseReceipt` $\to$ `PurchaseInvoice`.
- **Accrual Interim Liability:** Goods arrival creates interim liability `Stock Received But Not Billed`.

#### Scenario BY-01: Three-Way Matching & Bill Approval
- **Given** a `PurchaseReceipt` for 10 units @ $100.00 posted against `Stock Received But Not Billed` ($1,000.00)
- **When** the supplier's `PurchaseInvoice` arrives for 10 units @ $100.00 + $100.00 VAT
- **Then** posting the invoice debits `Stock Received But Not Billed` ($1,000.00), debits `Input Tax Recoverable` ($100.00)
- **And** credits `Accounts Payable` ($1,100.00), clearing the interim liability completely.

---

## 7. Module 6: Manufacturing & Scheduling

*ERPNext Parity: `erpnext/manufacturing/doctype` & `engine.py`*

- **`BOM` (Bill of Materials):** Hierarchical engineering recipe defining raw material requirements, scrap allowances, and operations cost.
- **`WorkOrder`:** Production order scheduling execution dates, tracking WIP (Work In Progress), and consuming stock.

#### Scenario MF-01: Production Run & WIP Stock Transfer
- **Given** a submitted `WorkOrder` for 10 units of `Finished Assembly` with an active BOM
- **When** raw materials are issued to production
- **Then** stock entries transfer inventory from `Raw Material Store` to `Work In Progress Warehouse`
- **And** upon completion of the manufacturing run, stock is transferred to `Finished Goods Store` with actualized manufacturing cost (materials + direct labor + overhead absorption).
