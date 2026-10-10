# Plan: Aspire ERP Module Review Against ERPNext via MCP

## Objective
Systematically review each certified Aspire ERP module against ERPNext doctypes using the `agent-reach` skill (ERPNext MCP) to identify:
- Missing fields, rules, validations, workflows
- Architectural differences
- Carry-forward gaps not captured in current roadmap

---

## Current Module Inventory (from roadmap.md & spec.md)

| # | Module | Certification | Key Entities |
|---|--------|---------------|--------------|
| 01 | Accounting & GL | CERTIFIED — amended scope | Account, GLEntry, JournalEntry, Company, FiscalYear, PeriodClosingVoucher |
| 02 | Stock & Inventory (FIFO) | 100% CERTIFIED | Item, Warehouse, UOM, StockEntry, StockLedgerEntry |
| 03 | Selling & POS | CERTIFIED — amended scope | Customer, Quotation, SalesOrder, DeliveryNote, SalesInvoice, POSProfile |
| 04 | Buying & Procurement | 100% CERTIFIED | Supplier, PurchaseOrder, PurchaseReceipt, PurchaseInvoice |
| 05 | Banking | 100% CERTIFIED | BankAccount, BankTransaction, BankReconciliation, BankTransactionRule |
| 06 | Manufacturing | 100% CERTIFIED | BillOfMaterials, BomItem, BomOperation, WorkOrder, Workstation |
| 07 | Assets | 100% CERTIFIED | Asset, AssetCategory, AssetDepreciationSchedule |
| 08 | CRM | CERTIFIED — with warnings | Lead, Opportunity, CRMActivity |
| 09 | HR & Payroll | CERTIFIED — with warnings | Employee, SalaryComponent, SalaryStructure, SalaryStructureAssignment, SalarySlip, PayrollEntry |

**In Progress / Pending:**
- R-12: Payment Entry API (domain exists, no controller)
- R-13: Fiscal Year + Period Closing Voucher
- R-14: Multi-currency FX Revaluation
- RM-01: Master Data UI (verified)

**Missing from ERPNext (Gap Matrix):**
- Tax Templates / Tax Withholding
- Quotation, Pricing Rules
- Material Request, Supplier Quotation
- Batch/Serial No, Stock Reconciliation
- Projects/Tasks/Timesheets
- Attendance/Leave
- RBAC, Naming Series, Workflow, Audit Trail

---

## Review Methodology

### Phase 1: Module-by-Module MCP Search (agent-reach)

For each module, use `agent-reach` with the `dev` (GitHub) category to search `frappe/erpnext` for:
1. **Doctype definition** - fields, field types, options, defaults
2. **Controller logic** - validate, on_submit, on_cancel, on_trash, custom methods
3. **Workflow/State transitions** - DocStatus (Draft/Submitted/Cancelled), custom workflows
4. **Permissions** - role-based, user-based, document-level
5. **Naming series** - format, options
6. **Child tables** - structure, mandatory fields
7. **Links/References** - to other doctypes
8. **Triggers/Hooks** - before_save, after_insert, etc.

### Phase 2: Comparison Matrix per Module

Create a comparison document for each module:

| Aspect | ERPNext | Aspire ERP | Gap | Severity |
|--------|---------|------------|-----|----------|
| Fields | List all | List all | Missing/Extra | Critical/High/Med/Low |
| Validations | List rules | List rules | Missing rules | Critical/High/Med/Low |
| Workflow | States, transitions | States, transitions | Missing transitions | High/Med |
| Permissions | Role matrix | Current impl | Gaps | Med/Low |
| Architecture | Frappe patterns | DDD/Clean Arch | Differences | Architectural |

### Phase 3: Cross-Cutting Concerns

Review these across ALL modules:
- **Naming Series** - per doctype, per company
- **Attachments/Files** - file upload, linking
- **Comments/Communications** - timeline, emails
- **Versioning/Audit Trail** - who/what/when
- **Print Formats** - standard, custom
- **Reports** - standard reports per module
- **Dashboard/Workspace** - ERPNext workspace concept
- **Translations** - field labels, select options

---

## Execution Steps

### Step 1: Accounting & GL (Module 01) - Priority: HIGH
**ERPNext Doctypes to search:**
- `Account` (Chart of Accounts)
- `GL Entry`
- `Journal Entry`
- `Fiscal Year`
- `Period Closing Voucher`
- `Exchange Rate Revaluation`
- `Tax Withholding Category`
- `Sales/Purchase Taxes and Charges Template`
- `Cost Center`
- `Accounting Dimension`

**Key areas to verify:**
- Account types, root types, group/leaf logic
- GL Entry immutability, indexes
- Journal Entry workflow (Draft → Submitted → Cancelled)
- Fiscal Year period locking
- Period Closing Voucher: retained earnings, P&L clearing
- Multi-currency: realized/unrealized FX
- Tax templates: item groups, tax categories, tax rules

### Step 2: Stock & Inventory (Module 02) - Priority: HIGH
**ERPNext Doctypes:**
- `Item` (with variants, batch/serial)
- `Warehouse` (hierarchy, account mapping)
- `UOM` (conversions)
- `Stock Entry` (types: Material Receipt, Issue, Transfer, Manufacture, Repack, etc.)
- `Stock Ledger Entry` (valuation, FIFO)
- `Stock Reconciliation`
- `Batch`, `Serial No`
- `Delivery Note`, `Purchase Receipt`

**Key areas:**
- Item valuation methods (FIFO, Moving Average, Standard)
- Warehouse account resolution through hierarchy
- Stock Entry purposes and their GL impact
- FIFO layer consumption logic
- Batch/Serial tracking through all movements
- Stock Reconciliation with GL impact

### Step 3: Selling & POS (Module 03) - Priority: HIGH
**ERPNext Doctypes:**
- `Customer` (credit limit, payment terms, territory, customer group)
- `Quotation` → `Sales Order` conversion
- `Sales Order` (delivery dates, reserved stock)
- `Delivery Note` (against SO, FIFO)
- `Sales Invoice` (against SO/DN, tax templates)
- `POS Profile`, `POS Invoice`
- `Pricing Rule`, `Price List`
- `Sales Taxes and Charges Template`

**Key areas:**
- Credit limit enforcement (incl. override)
- Quotation validity, conversion to SO
- SO reserved stock, delivery scheduling
- DN against SO with FIFO valuation
- SI posting: A/R, Revenue, Tax, COGS
- POS: payments, payments modes, loyalty
- Pricing: price lists, pricing rules, discounts

### Step 4: Buying & Procurement (Module 04) - Priority: HIGH
**ERPNext Doctypes:**
- `Supplier` (credit limit, payment terms, supplier group)
- `Material Request` (Material Issue, Purchase, etc.)
- `Supplier Quotation` → `Purchase Order`
- `Purchase Order` (schedule, terms)
- `Purchase Receipt` (against PO, quality inspection)
- `Purchase Invoice` (against PO/PR, tax withholding)
- `Purchase Taxes and Charges Template`
- `Tax Withholding Category`

**Key areas:**
- MR types and workflow
- SQ comparison, PO creation
- PO: billing vs delivery, progress tracking
- PR: inspection, rejection, acceptance
- PI three-way match, tax withholding
- Return: stock reversal, credit note

### Step 5: Banking (Module 05) - Priority: MEDIUM
**ERPNext Doctypes:**
- `Bank Account` (account, currency, balance)
- `Bank Transaction` (staging, matching)
- `Bank Reconciliation Tool` (statement import, reconciliation)
- `Bank Transaction Rule` (auto-match)
- `Payment Entry` (receive, pay, internal transfer)
- `Payment Request`

**Key areas:**
- Bank statement import (CSV, OFX, MT940)
- Auto-reconciliation rules
- Manual reconciliation UI
- Payment Entry: modes, allocation, advances
- Cheque management (PDC, clearance)

### Step 6: Manufacturing (Module 06) - Priority: MEDIUM
**ERPNext Doctypes:**
- `BOM` (with/without operations, scrap, sub-assemblies)
- `Work Order` (from SO, scheduling, material transfer)
- `Job Card` (operations, time tracking, completion)
- `Workstation` (capacity, holiday list)
- `Production Plan` (from SO, MRP)
- `Quality Inspection` (incoming, in-process, final)

**Key areas:**
- BOM structure: items, operations, scrap %
- WO: material transfer to WIP, job cards
- Job Card: time logs, scrap, completion
- Workstation scheduling, capacity
- Production Plan from SO/MRP
- Scrap valuation account flow

### Step 7: Assets (Module 07) - Priority: MEDIUM
**ERPNext Doctypes:**
- `Asset Category` (depreciation method, accounts, frequency)
- `Asset` (location, custodian, depreciation schedule)
- `Asset Depreciation Schedule` (auto-generated)
- `Asset Movement` (transfer, location change)
- `Asset Maintenance` (schedule, logs)
- `Asset Repair` (cost, vendor)
- `Asset Value Adjustment` (revaluation, impairment)
- `Asset Disposal` (sale, scrap, write-off)

**Key areas:**
- Depreciation methods: SL, DDB, WDV, Manual
- Schedule generation, posting
- Movement tracking
- Maintenance scheduling
- Repair vs Capitalization
- Revaluation/Impairment with GL
- Disposal: gain/loss calculation

### Step 8: CRM (Module 08) - Priority: LOW
**ERPNext Doctypes:**
- `Lead` (source, status, territory)
- `Opportunity` (from lead, sales stage, probability)
- `Quotation` (linked to Opportunity)
- `Campaign` (email, ROI)
- `Contact`, `Address` (linked to Lead/Opp/Customer)
- `Activity` (Call, Meeting, Email, Note)

**Key areas:**
- Lead → Opportunity → Quotation → SO flow
- Sales stages, probability mapping
- Territory assignment
- Campaign tracking
- Activity timeline

### Step 9: HR & Payroll (Module 09) - Priority: LOW
**ERPNext Doctypes:**
- `Employee` (department, designation, grade, employment type)
- `Salary Component` (earning/deduction, formula, condition)
- `Salary Structure` (components, payroll frequency)
- `Salary Structure Assignment` (employee, effective date)
- `Salary Slip` (earnings, deductions, loan, tax)
- `Payroll Entry` (bulk processing)
- `Employee Benefit Application` (loan, advance)
- `Leave Application`, `Leave Allocation`, `Leave Type`
- `Attendance` (check-in/out, shift)
- `Shift Type`, `Holiday List`

**Key areas:**
- Salary component formulas (condition, eval)
- Structure assignment versioning
- Slip calculation: earnings, deductions, tax
- Payroll entry: bulk, journal posting
- Leave: allocation, encashment, carry-forward
- Attendance: shifts, overtime, late/early

---

## Tooling

**Primary:** `agent-reach` skill with `dev` category (GitHub search on `frappe/erpnext`)
- Use `search_erpnext_code` for doctype controllers
- Use `get_erpnext_docs` for official documentation

**Secondary:** Read Aspire ERP source:
- Domain entities: `src/Backend/Erp.Domain/Entities/`
- Application handlers: `src/Backend/Erp.Application/Features/`
- API controllers: `src/Backend/Erp.Api/Controllers/V1/`
- Frontend: `src/Frontend/erp-client/src/features/`

---

## Deliverables

1. **Per-module comparison report** (Markdown) saved to `.specify/reviews/<module>-review.md`
2. **Consolidated gap matrix** updating `roadmap.md` Section 3
3. **Updated carry-forwards** in `roadmap.md` Section 2
4. **New roadmap items** for any critical missing functionality

---

## Questions for Clarification

Before proceeding, I need to confirm:

1. **Scope**: Review all 9 certified modules, or only specific ones?
2. **Depth**: Full field-by-field comparison, or focus on high-risk areas (workflows, validations, GL postings)?
3. **Output format**: Separate review files per module, or single consolidated report?
4. **Priority order**: Follow roadmap priority (Accounting → Stock → Selling → Buying → ...) or alphabetical?
5. **MCP usage**: Use `agent-reach` for each doctype search, or batch searches?

---

## Recommended Approach

**Start with Module 01 (Accounting & GL)** as it's the foundation for all other modules and has the most deferred items (FX, Period Closing, Tax). Then proceed in roadmap priority order.

**Use agent-reach in batches** - search 3-5 doctypes per module per invocation to be efficient.

**Output**: One review file per module in `.specify/reviews/` + updated `roadmap.md`.

---

## Acceptance Criteria

- [ ] Each certified module has a completed comparison report
- [ ] All `to verify` items in roadmap.md Section 3 are confirmed
- [ ] New gaps are added to roadmap with severity
- [ ] Carry-forwards in Section 2 are updated
- [ ] Architectural differences documented (Frappe DocType vs DDD Aggregate)