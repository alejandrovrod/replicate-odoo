# Implementation Tasks: Multi-Tenant Cloud ERP Core (ERPNext Parity)

**Status:** READY FOR EXECUTION  
**Format:** Spec Kit Task Checklist (Phase 0 to Phase 8)  
**Version:** 2.0.0  

This document provides granular, atomic, testable tasks for human engineers and AI coding subagents. Each task must satisfy its Acceptance Criteria before being checked off.

### Modular Task Roadmaps (GitHub Spec Kit Triad: Spec · Plan · Tasks):
| Module | Certification | Functional Spec | Technical Plan | Tasks Roadmap |
| :--- | :---: | :--- | :--- | :--- |
| **01. Accounting & General Ledger** | `CERTIFIED — amended scope` | [spec.md](./modules/archive/2026-10-04-01-accounting/spec.md) | [plan.md](./modules/archive/2026-10-04-01-accounting/plan.md) | [tasks.md](./modules/archive/2026-10-04-01-accounting/tasks.md) |
| **02. Stock & Inventory (Kardex FIFO)** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-04-02-stock/spec.md) | [plan.md](./modules/archive/2026-10-04-02-stock/plan.md) | [tasks.md](./modules/archive/2026-10-04-02-stock/tasks.md) |
| **03. Selling & Point of Sale (POS)** | `CERTIFIED — amended scope` | [spec.md](./modules/archive/2026-10-04-03-selling/spec.md) | [plan.md](./modules/archive/2026-10-04-03-selling/plan.md) | [tasks.md](./modules/archive/2026-10-04-03-selling/tasks.md) |
| **04. Buying & Procurement** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-03-04-buying/spec.md) | [plan.md](./modules/archive/2026-10-03-04-buying/plan.md) | [tasks.md](./modules/archive/2026-10-03-04-buying/tasks.md) |
| **05. Banking & Reconciliation Subsystem** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-04-05-banking/spec.md) | [plan.md](./modules/archive/2026-10-04-05-banking/plan.md) | [tasks.md](./modules/archive/2026-10-04-05-banking/tasks.md) |
| **06. Manufacturing & Production (BOM)** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-04-06-manufacturing/spec.md) | [plan.md](./modules/archive/2026-10-04-06-manufacturing/plan.md) | [tasks.md](./modules/archive/2026-10-04-06-manufacturing/tasks.md) |
| **07. Asset Management & Depreciation** | `100% CERTIFIED` | [spec.md](./modules/archive/2026-10-04-07-assets/spec.md) | [plan.md](./modules/archive/2026-10-04-07-assets/plan.md) | [tasks.md](./modules/archive/2026-10-04-07-assets/tasks.md) |
| **08. CRM & Sales Pipeline** | `100% CERTIFIED` | [spec.md](./modules/08-crm/spec.md) | [plan.md](./modules/08-crm/plan.md) | [tasks.md](./modules/08-crm/tasks.md) |
| **09. Human Resources & Payroll** | `CERTIFIED — with warnings` | [spec.md](./modules/archive/2026-10-04-09-hr-payroll/spec.md) | [plan.md](./modules/archive/2026-10-04-09-hr-payroll/plan.md) | [tasks.md](./modules/archive/2026-10-04-09-hr-payroll/tasks.md) |

---

## Phase 0: Scaffolding & .NET Aspire Setup

- [x] **Task 0.1: Initialize Solution & Projects**
  - **Action:** Create `Erp.sln` and scaffold Clean Architecture projects:
    - `src/Backend/Erp.Domain` (Classlib, .NET 9/10)
    - `src/Backend/Erp.Application` (Classlib)
    - `src/Backend/Erp.Infrastructure` (Classlib)
    - `src/Backend/Erp.Api` (Web API)
    - `src/Backend/Erp.ServiceDefaults` (Aspire ServiceDefaults)
    - `src/Backend/Erp.AppHost` (Aspire AppHost)
  - **Acceptance:** Solution compiles with zero warnings; layer dependencies strictly respect the Constitution (Domain has zero references).

- [x] **Task 0.2: Configure Aspire Orchestrator**
  - **Action:** In `Erp.AppHost/Program.cs`, register official SQL Server 2025 container (`mcr.microsoft.com/mssql/server:2025-latest`), configure persistent volume, register Redis container, and bind `Erp.Api`.
  - **Acceptance:** Running `dotnet run --project src/Backend/Erp.AppHost` launches the Aspire Dashboard with green health checks for SQL Server and Redis.

- [x] **Task 0.3: Initialize React Frontend Application**
  - **Action:** Scaffold `src/Frontend/erp-client` using Vite + React 19 + TypeScript. Install Tailwind CSS, Radix UI primitives, Lucide icons, Zustand, and Axios.
  - **Acceptance:** Frontend builds cleanly via `npm run build` and dev server loads at `localhost:5173`.

---

## Phase 1: Multi-Tenancy Core & Ambient Logging

- [x] **Task 1.1: Implement Tenant Abstractions & Middleware**
  - **Action:** In `Erp.Application`, define `ITenantEntity` and `ITenantProvider`. In `Erp.Api`, create `TenantResolutionMiddleware` extracting tenant from `X-Tenant-ID` header or JWT claim.
  - **Acceptance:** Scoped service returns valid `TenantId` when header is passed; returns 401/400 if tenant is missing on protected routes.

- [x] **Task 1.2: Implement Multi-Tenant Logging Scope Middleware**
  - **Action:** Create `TenantLoggingScopeMiddleware` opening `_logger.BeginScope` with `TenantId`, `CorrelationId`, and `UserId`.
  - **Acceptance:** Every log emitted during a request automatically includes `TenantId` in structured JSON/OpenTelemetry format.

- [x] **Task 1.3: Implement `AppDbContext` with Dynamic Global Query Filters**
  - **Action:** In `Erp.Infrastructure`, configure EF Core `AppDbContext` to dynamically apply `HasQueryFilter` on all `ITenantEntity` models via Expression Trees. Add `SaveChangesAsync` interceptor enforcing immutable `TenantId`.
  - **Acceptance:** Queries automatically append `WHERE TenantId = @id`; attempts to update `TenantId` throw `InvalidOperationException`.

- [x] **Task 1.4: Initial Database Migration**
  - **Action:** Create EF Core migration for `Tenants` and `Companies` tables.
  - **Acceptance:** Migration executes successfully on SQL Server 2025 container.

---

## Phase 2: Master Data & Chart of Accounts (COA)

- [x] **Task 2.1: Domain Model `Account` & Hierarchical Tree**
  - **Action:** In `Erp.Domain`, create `Account` entity (Asset, Liability, Equity, Income, Expense, ParentAccountId, IsGroup).
  - **Acceptance:** Unit tests verify validation logic (e.g. root account cannot have invalid parent).

- [x] **Task 2.2: Configure SQL Server 2025 Temporal Table**
  - **Action:** In `Erp.Infrastructure`, configure `AccountConfiguration` using EF Core `.IsTemporal(t => t.UseHistoryTable("AccountHistory"))`.
  - **Acceptance:** Updating an account code preserves previous record in `AccountHistory` with valid UTC timestamp period.

- [x] **Task 2.3: CQRS Queries & Tree Builder**
  - **Action:** In `Erp.Application`, implement `CreateAccountCommand` and `GetAccountTreeQuery`.
  - **Acceptance:** `GetAccountTreeQuery` returns hierarchical nested JSON structure of accounts.

- [x] **Task 2.4: React `AccountTreeTable` UI**
  - **Action:** In `erp-client/src/features/accounting`, build the collapsible tree-table with expand/collapse and inline status indicators.
  - **Acceptance:** Tree displays hierarchical accounts correctly with zero layout shift.

---

## Phase 3: Stock & Inventory Module (ERPNext Parity)

- [x] **Task 3.1: Domain Models `Item`, `Warehouse`, and `UOM`**
  - **Action:** In `Erp.Domain`, implement `Item` (SKU, valuation method, income/expense accounts), `Warehouse` (parent-child locations, linked stock account), and `UOM` with conversion factors.
  - **Acceptance:** Items validate unique SKU per tenant; Warehouses enforce tree structure.

- [x] **Task 3.2: Perpetual Inventory Engine (`StockLedgerEntry`)**
  - **Action:** Implement `StockEntry` (Material Receipt, Issue, Transfer) and `StockLedgerEntry` (Kardex FIFO valuation).
  - **Acceptance:** Moving stock immediately writes balanced General Ledger entries (Debit Inventory, Credit Adjustment / Expense).

- [x] **Task 3.3: Negative Stock Validation**
  - **Action:** Enforce anti-negative stock rule if company policy forbids negative inventory.
  - **Acceptance:** Attempting to issue more stock than currently available in warehouse throws `InsufficientStockException`.

- [x] **Task 3.4: React Inventory UI (Item List & Warehouse View)**
  - **Action:** Build `ItemList` with stock levels and `StockEntryModal` in `erp-client/src/features/stock`.
  - **Acceptance:** Users can create stock entries and view updated stock levels in real time.

---

## Phase 4: Buying Cycle (Compras & Aprovisionamiento)

- [x] **Task 4.1: Domain Models `Supplier`, `PurchaseOrder`, and `PurchaseReceipt`**
  - **Action:** In `Erp.Domain`, implement `Supplier`, `PurchaseOrder`, `PurchaseReceipt`, and `PurchaseInvoice`.
  - **Acceptance:** Entities support multi-step procurement workflow (`Draft` -> `Ordered` -> `Received` -> `Billed`).

- [x] **Task 4.2: Accrual Accounting on Goods Receipt (Interim Liability)**
  - **Action:** When `PurchaseReceipt` posts:
    - Debit: Stock In Hand (Inventory Asset)
    - Credit: Stock Received But Not Billed (Interim Liability).
  - **Acceptance:** Warehouse quantities increase and interim liability account reflects received goods value.

- [x] **Task 4.3: Vendor Bill Clearance (`PurchaseInvoice`)**
  - **Action:** Posting `PurchaseInvoice` clears the interim liability and books Accounts Payable + Input VAT.
  - **Acceptance:** `Stock Received But Not Billed` balance zeroes out for matching quantities; Accounts Payable reflects vendor debt.

---

## Phase 5: Selling Cycle (Ventas & Clientes)

- [ ] **Task 5.1: Domain Models `Customer`, `SalesOrder`, `DeliveryNote`, and `SalesInvoice`**
  - **Action:** In `Erp.Domain`, implement `Customer`, `SalesOrder`, `DeliveryNote`, and `SalesInvoice`.
  - **Acceptance:** Three-way matching tracks `DeliveredQuantity` and `BilledQuantity` per order line.

- [ ] **Task 5.2: Delivery Fulfillment & COGS Posting**
  - **Action:** Posting `DeliveryNote` deducts stock in `StockLedgerEntry` and creates General Ledger entry:
    - Debit: Cost of Goods Sold (COGS)
    - Credit: Stock In Hand.
  - **Acceptance:** Physical stock is deducted; COGS is recognized on fulfillment date.

- [ ] **Task 5.3: Sales Invoice Posting & Credit Control**
  - **Action:** Posting `SalesInvoice` verifies customer credit limit, generates sequential number (`SINV-2026-XXXX`), and posts balanced entries (Debit Accounts Receivable, Credit Revenue, Credit Tax).
  - **Acceptance:** Invariant $\sum \text{Debit} == \sum \text{Credit}$ verified; credit limit breach blocks invoice submission.

- [ ] **Task 5.4: React Sales Studio UI**
  - **Action:** Build `SalesOrderList`, `DeliveryNoteModal`, and `InvoiceStudio` with live calculations in React.
  - **Acceptance:** Users can convert a Sales Order into a Delivery Note and Sales Invoice with 1 click.

---

## Phase 6: Treasury, Payments & Banking Subsystem (ERPNext Parity)

- [ ] **Task 6.1: Domain Models `PaymentEntry` and `PaymentAllocation`**
  - **Action:** Create `PaymentEntry` (PaymentType: Receive/Pay, PaidAmount, BankAccount) and `PaymentAllocation`.
  - **Acceptance:** Validates anti-overpayment invariant (`AllocatedAmount <= Invoice.OutstandingAmount`). Posting debits Bank, credits A/R, updates invoice balance, calculates realized FX gain/loss if currencies differ, and reserves surplus as Customer Advance.

- [ ] **Task 6.2: Bank Statement Import & Staging Engine**
  - **Action:** Create `BankStatementImport` and `BankTransaction` entities. Implement parser services for CSV and OFX formats in `Erp.Application.Banking`.
  - **Acceptance:** Statement lines are imported directly into `BankTransaction` in `Unreconciled` status. Strictly enforces the **Staging Isolation Invariant**: zero accounting entries are posted to `GLEntry`.

- [ ] **Task 6.3: Heuristic Rules Engine (`BankTransactionRule`)**
  - **Action:** Implement `IBankTransactionRuleEvaluator` executing configured rules by priority against description regexes, substrings, and transaction amounts.
  - **Acceptance:** Auto-populates Party, Account, and marks transactions as `Matched` or triggers auto-voucher creation.

- [ ] **Task 6.4: Bank Reconciliation Service (`BankReconciliationTool`)**
  - **Action:** Implement `ReconcileBankTransactionCommand` matching staging transactions against existing `PaymentEntry` or `GLEntry` records.
  - **Acceptance:** Updates `BankTransaction.Status` to `Reconciled`, stamps `ClearanceDate`, and verifies that the Bank Reconciliation Statement difference equals $0.00.

- [ ] **Task 6.5: On-The-Fly Voucher Dialog Backend (`DialogManager`)**
  - **Action:** Implement `CreateVoucherFromBankTransactionCommand` to allow instant creation of Journal Entries or expense payments directly from an unmatched bank transaction line.
  - **Acceptance:** Creates balanced `GLEntry` and reconciles the bank line atomically in a single transaction.

- [ ] **Task 6.6: React Banking Subsystem UI (SPA Parity)**
  - **Action:** Build `banking/src/App.tsx`, `BankStatementImporter.tsx` (drag-and-drop file upload with column mapping preview), `BankReconciliation.tsx` (dual-sided split comparison grid), and `VoucherQuickCreateDialog.tsx`.
  - **Acceptance:** Users can import statements, view matched suggestions, filter unreconciled transactions, open the quick voucher dialog, and reconcile in one click.

---

## Phase 7: Financial Reporting & Executive Dashboard (ShadcnBlocks)

- [ ] **Task 7.1: Financial Reporting CQRS Queries**
  - **Action:** Implement queries:
    - `GetBalanceSheetQuery` (Assets = Liabilities + Equity)
    - `GetProfitAndLossQuery` (Revenue - COGS - Expenses = Net Profit)
    - `GetTrialBalanceQuery` (Debits == Credits verification)
    - `GetStockLedgerReportQuery` (Kardex valuation per warehouse/item)
    - `GetAgingReportQuery` (Receivables/Payables 0-30, 31-60, 61-90, 90+ days).
  - **Acceptance:** Balance Sheet balances; Trial Balance reports zero discrepancy.

- [ ] **Task 7.2: Integrate ShadcnBlocks Dashboard Template**
  - **Action:** In `erp-client/src/features/dashboard`, implement `KpiCardsGrid`, `RevenueAreaChart`, and `RecentInvoicesList`.
  - **Acceptance:** Dashboard renders real-time data from backend with responsive design.

---

## Phase 8: Verification, Security & Production Hardening

- [ ] **Task 8.1: Multi-Tenant Adversarial Tests**
  - **Action:** Write integration tests simulating 5 tenants submitting concurrent orders, invoices, and payments.
  - **Acceptance:** 100% of queries return strictly tenant-isolated records; zero leakage.

- [ ] **Task 8.2: End-to-End Ledger Integrity Stress Test**
  - **Action:** Execute automated script running complete procurement and sales cycles (PO -> Receipt -> Bill -> SO -> Delivery -> Invoice -> Payment).
  - **Acceptance:** Global sum of all `GLEntry` debits minus credits equals exactly `0.0000`; inventory valuation in GL matches physical stock ledger.
