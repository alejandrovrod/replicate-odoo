# Aspire ERP — ERPNext Parity Roadmap & Gap Matrix

**Status:** DRAFT — pending review  
**Snapshot date:** 2026-10-05  
**Baseline commit:** `fc9fec99` (08-crm archived)  
**Reference source:** `frappe/erpnext` via `erpnext-docs` MCP (`search_erpnext_code`)  

This document is the single ordered backlog for taking Aspire ERP from "9 modules certified"
to full ERPNext functional parity. It does NOT replace the per-module SDD triad
(spec · plan · tasks). Each row below becomes its own SDD change when it is picked up.

---

## 1. How to read this document

- **Certified** = module archived through SDD with a verify report.
- **Carry-forward** = scope explicitly DEFERRED or accepted as a WARNING in an archive report.
  Carry-forwards are debt inside certified modules, not new features.
- **ERPNext ref** marked `verified` was confirmed to exist in `frappe/erpnext` through the MCP.
  Refs marked `to verify` must be confirmed with `search_erpnext_code` before writing the spec.
- **Ordering rule:** a row may only start when every row in its `Depends on` column is archived.
  Everything posts to the General Ledger, so accounting debt always goes first.

---

## 2. Current state matrix (as built)

| # | Module | Certification | Domain | API (write) | UI | Main carry-forwards |
|---|--------|---------------|:------:|:-----------:|:--:|---------------------|
| 00 | i18n | IN PROGRESS (active change) | — | — | partial | Uncommitted edits across all V1 controllers + `Program.cs` |
| 01 | Accounting & GL | CERTIFIED — amended scope | yes | yes | yes | FX gain/loss (AC-05), period closing / fiscal year (AC-06), JE create endpoint not idempotency-guarded |
| 02 | Stock (FIFO) | 100% CERTIFIED | yes | yes | yes | Warehouse account resolution through hierarchy (W4), `StockLedgerEntry` immutability not DB-guarded (W8) |
| 03 | Selling & POS | CERTIFIED — amended scope | yes | partial | partial | **Sales Invoice posting unrouted**, tax forced to 0, POS not end-to-end, no invoice cancel, credit exposure via invoice, delivery-note FIFO read without range lock |
| 04 | Buying | 100% CERTIFIED | yes | yes | yes | Tax withholding (W4), purchase return stock reversal (W1), PO progress fields never set (W5), partial bill marks PO `Completed` (W6), no `RowVersion` on invoice DTO (W8) |
| 05 | Banking | 100% CERTIFIED | yes | yes | yes | UI behaviour build-verified only (no frontend test runner) |
| 06 | Manufacturing | 100% CERTIFIED | yes | partial | yes | No BOM write path (read-only API/UI), scrap valuation hard-fails, job-card actual costing deferred |
| 07 | Assets | 100% CERTIFIED | yes | yes | no | No UI task |
| 08 | CRM | CERTIFIED — with warnings | yes | yes | yes | Opportunity → Sales Order link is note-based (no FK) |
| 09 | HR & Payroll | CERTIFIED — with warnings | yes | partial | yes | Salary component / structure / assignment create handlers have no HTTP routes; replay and race tests only at fake level |

Cross-cutting findings:

- `PaymentEntry` / `PaymentAllocation` exist as domain entities, but **no controller exposes them**.
  Receivables and payables therefore cannot be settled through the API.
- The master [`tasks.md`](./tasks.md) still shows Phases 5–8 unchecked although modules 03, 05 and 06–09
  are archived. It must be reconciled (see R-00).
- No frontend test runner exists. Every UI task so far was verified by `tsc` + `vite build` + lint only.

---

## 3. Gap matrix against ERPNext

Doctypes probed in `src/Backend` (class/record name search, excluding migrations) on 2026-10-05.

| Area | ERPNext doctype | ERPNext ref | Aspire status |
|------|-----------------|-------------|---------------|
| Accounting | Payment Entry | `erpnext/accounts/doctype/payment_entry` — verified | Domain only, no API |
| Accounting | Period Closing Voucher | `erpnext/accounts/doctype/period_closing_voucher` — verified | Missing |
| Accounting | Fiscal Year | to verify | Missing |
| Accounting | Exchange Rate Revaluation | `erpnext/accounts/doctype/exchange_rate_revaluation` — verified | Missing |
| Accounting | Tax Withholding Category | `erpnext/accounts/doctype/tax_withholding_category` — verified | Missing (hard-coded 0) |
| Accounting | Sales/Purchase Taxes and Charges Template | to verify | Missing (tax forced to 0 in selling) |
| Selling | Quotation | to verify | Missing |
| Selling | Pricing Rule | to verify | Missing |
| Buying | Material Request | to verify | Missing |
| Buying | Supplier Quotation | to verify | Missing |
| Stock | Batch / Serial No | to verify | Missing |
| Stock | Stock Reconciliation | to verify | Missing |
| Projects | Project / Task / Timesheet | to verify | Missing |
| HR | Attendance / Leave Application | to verify | Missing |
| Platform | Role / DocType permissions | to verify (Frappe core) | Partial (1 `Role` type found) |
| Platform | Workflow | to verify (Frappe core) | Missing |
| Platform | Naming Series | to verify (Frappe core) | Missing |
| Platform | Version / audit trail | to verify (Frappe core) | Partial (temporal tables on some entities) |

---

## 4. Ordered roadmap

### Phase R0 — Housekeeping (no new behaviour)

| ID | Task | Depends on | Source |
|----|------|------------|--------|
| R-00 | Reconcile master `tasks.md` / `plan.md` / `spec.md` indexes with the archive state | — | Drift found in this audit |
| R-01 | Finish and archive `00-i18n` (commit the pending controller edits) | — | Active change |
| R-02 | Add a frontend test runner (Vitest + Testing Library) and a smoke test per feature | — | Carry-forward S2 (assets/banking/manufacturing) |

### Phase R1 — Close the accounting loop (highest priority)

Without these, A/R and A/P are open forever and the GL cannot be closed. Every later phase depends on them.

| ID | Task | Depends on | ERPNext ref | Source |
|----|------|------------|-------------|--------|
| R-10 | Tax templates (sales + purchase) and real tax calculation on invoices | R-01 | to verify | Selling W6, SL-01 deferred |
| R-11 | Route and certify Sales Invoice create/submit/cancel with A/R + revenue + tax posting, idempotency, customer outstanding update | R-10 | to verify | Selling SL-01, SL-03..SL-06 deferred |
| R-12 | Payment Entry API (receive/pay, allocation against invoices, anti-overpayment, advances) | R-11 | `payment_entry` — verified | Cross-cutting finding |
| R-13 | Fiscal Year + Period Closing Voucher + hard period lock + retained earnings account | R-12 | `period_closing_voucher` — verified | Accounting AC-06 deferred |
| R-14 | Multi-currency: exchange rates, realized FX on payment, unrealized via revaluation | R-12 | `exchange_rate_revaluation` — verified | Accounting AC-05 deferred |
| R-15 | Tax withholding categories applied on purchase invoice/payment | R-10, R-12 | `tax_withholding_category` — verified | Buying W4 |
| R-16 | Financial reports completion: aging (A/R, A/P), stock ledger report, BS/P&L over closed periods | R-13 | to verify | Master tasks Phase 7.1 |

### Phase R2 — Close certified-module debt

| ID | Task | Depends on | Source |
|----|------|------------|--------|
| R-20 | POS end-to-end: seeded profile/customer, tax-correct tender, COGS + stock relief, tests | R-11 | Selling POS deferred |
| R-21 | Delivery-note FIFO read under the stock range lock (oversell race) | — | Selling carry-forward safety flag |
| R-22 | Purchase returns with stock reversal; fix PO progress fields and partial-bill status | — | Buying W1, W5, W6, W8 |
| R-23 | Warehouse account resolution through hierarchy; DB guard for `StockLedgerEntry` immutability | — | Stock W4, W8 |
| R-24 | BOM write path (create/submit API + UI) and scrap valuation account/flow | — | Manufacturing W2, W4 |
| R-25 | HR masters write routes (components, structures, assignments) with idempotency | — | HR-Payroll W4 |
| R-26 | Assets UI | — | Assets W6 |
| R-27 | `OpportunityId` FK on Sales Order | — | CRM S-LINKAGE |

### Phase R3 — Platform capabilities (Frappe-core parity)

| ID | Task | Depends on | Source |
|----|------|------------|--------|
| R-30 | RBAC: roles, per-document permissions, row-level rules (own records, company) | R-01 | Gap matrix |
| R-31 | Naming series per document type and company | — | Gap matrix |
| R-32 | Generic audit trail (who/what/when) for all submittable documents | — | Gap matrix |
| R-33 | Configurable approval workflow (draft → approval → submitted) | R-30 | Gap matrix |

### Phase R4 — Pre-transaction documents

| ID | Task | Depends on | Source |
|----|------|------------|--------|
| R-40 | Quotation → Sales Order conversion | R-11 | Gap matrix |
| R-41 | Pricing rules and price lists | R-40 | Gap matrix |
| R-42 | Material Request → Supplier Quotation → Purchase Order | R-22 | Gap matrix |

### Phase R5 — Advanced stock

| ID | Task | Depends on | Source |
|----|------|------------|--------|
| R-50 | Batch and serial number tracking through all stock movements | R-23 | Gap matrix |
| R-51 | Stock reconciliation (physical count adjustment with GL impact) | R-23 | Gap matrix |

### Phase R6 — New specialized modules

| ID | Task | Depends on | Source |
|----|------|------------|--------|
| R-60 | Projects, tasks and timesheets (billable time → Sales Invoice) | R-11 | Gap matrix |
| R-61 | HR attendance and leave (feeding payroll) | R-25 | Gap matrix |

### Phase R7 — Production hardening

| ID | Task | Depends on | Source |
|----|------|------------|--------|
| R-70 | Multi-tenant adversarial tests (5 concurrent tenants, zero leakage) | R1 done | Master tasks 8.1 |
| R-71 | End-to-end ledger integrity run (PO → receipt → bill → SO → delivery → invoice → payment → close) | R1 done | Master tasks 8.2 |
| R-72 | Executive dashboard on live data | R-16 | Master tasks 7.2 |

---

## 5. Working agreement per row

1. Confirm every `to verify` ERPNext reference with `search_erpnext_code` and read the doctype controller.
2. Open an SDD change (`/sdd-new <id>-<slug>`): proposal → spec → plan → tasks.
3. Implement, verify, archive. Record new carry-forwards back into section 2 of this file.
4. Update the row status here in the same commit that archives the change.
