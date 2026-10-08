# Functional Specification: Accounting & General Ledger (ERPNext Parity)

**Module:** `01-accounting`  
**Status:** CERTIFIED — scope amended 2026-10-04 after retro-verification; scope amended 2026-10-08 by R-13 (invariant AC-06 and scenario AC-05 now IMPLEMENTED, invariant AC-05 FX re-tracked to R-14)  
**Version:** 2.1.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext Accounting Documentation](https://docs.frappe.io/erpnext/accounting)  

---

## 1. Executive Summary & Ubiquitous Language

The **Accounting Module** is the foundational core of the ERP. Every business operational event—sales invoices, purchase receipts, inventory movements (Kardex FIFO), payments, and bank transactions—ultimately produces balanced financial movements recorded in the **General Ledger**.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Chart of Accounts (COA)** | `Account` | Hierarchical tree of financial accounts classified under 5 root types: Asset, Liability, Equity, Income, Expense. |
| **Group Account** | `Account.is_group = 1` | Non-posting folder used exclusively for grouping and summing sub-account balances. Direct postings are strictly forbidden. |
| **Posting Account** | `Account.is_group = 0` | Terminal leaf account where financial debits and credits are booked. |
| **General Ledger Entry** | `GL Entry` | Atomic, immutable ledger line recording debit or credit to an account for a specific voucher. |
| **Double-Entry Invariant** | Double-Entry Balance | For every posted voucher, the sum of all debits must equal the sum of all credits ($\sum \text{Debit} == \sum \text{Credit}$). |
| **Journal Entry** | `Journal Entry` | Multi-line voucher used for manual accounting adjustments, opening balances, bank transfers, write-offs, and inter-company movements. |
| **Payment Ledger Entry** | `Payment Ledger Entry` | Specialized ledger linking payments, receipts, and credit notes to specific invoice installments. |
| **Fiscal Year** | `Fiscal Year` | Accounting year boundary with defined start and end dates. |
| **Period Closing Voucher** | `Period Closing Voucher` | Year-end closing transaction transferring net balance of all Income and Expense accounts to Retained Earnings (Equity). |
| **Frozen Date / Period Lock** | `Accounts Settings.freeze_date` | Date boundary before which no accounting entries can be created, altered, or cancelled. |
| **Cost Center** | `Cost Center` | Dimensional tag tracking income and expense by department, project, or business unit. |

---

## 2. Core Business Invariants & Accounting Laws

### Invariant AC-01: Zero-Sum Double-Entry ($\sum D - \sum C == 0.0000$)
- In every voucher submitted to the General Ledger:
  $$\left| \sum \text{Debit} - \sum \text{Credit} \right| \le 0.0001$$
- If a discrepancy exists, the command fails with `DoubleEntryImbalanceException`.

### Invariant AC-02: Append-Only Immutability
- Records in `GLEntry` can **never** be updated (`UPDATE`) or deleted (`DELETE`).
- Cancellation of any transaction creates counter-entries reversing the debits and credits with an audit reference to the original voucher.

### Invariant AC-03: Leaf Posting Accounts Only
- Any attempt to post a `GLEntry` to an account where `Account.IsGroup == true` is rejected with `InvalidPostingAccountException`.

### Invariant AC-04: Hard Fiscal Period Lock (Freeze Date)
- If `PostingDate <= Company.FrozenAccountsDate`, all posting, modification, or cancellation is blocked with `FiscalPeriodLockedException`.

### Invariant AC-05: Realized Foreign Exchange Gain/Loss
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** no exchange-rate, settlement or FX posting path exists anywhere in the implementation, and no scenario exercises FX. Out of the certified scope of this module; tracked as a carry-forward flag in this module's archive report.
> **RE-TRACKED 2026-10-08 (S1):** still unimplemented — now owned by active change **R-14** (roadmap: multi-currency, realized FX on payment, unrealized via revaluation, ref `exchange_rate_revaluation`). This marker stays DEFERRED until R-14 archives.

- When transactions settle in foreign currencies at differing exchange rates, variance must post automatically to the predefined `Exchange Gain/Loss Account`:
  $$\text{RealizedFX} = \text{PaymentAmount}_{\text{FC}} \times (\text{Rate}_{\text{Settlement}} - \text{Rate}_{\text{Original}})$$

### Invariant AC-06: Period Closing Balance Roll-Forward
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** _(superseded — see below)_ no period-closing implementation existed at that date.
> **IMPLEMENTED 2026-10-08 by R-13** (archived `.specify/modules/archive/2026-10-08-13-fiscal-closing/`, verify PASS WITH WARNINGS): `FiscalYear` entity + lifecycle, `PeriodClosingVoucher` (Draft→Submitted→Cancelled, idempotent submit, reversal-only cancel), `PeriodClosingCalculator` (ΣD==ΣC ±0.0001), hard period lock in 21 posting pipelines, `3100 - Retained Earnings` seeded + company default. Back in certified scope.

- At fiscal year close, net profit transfers to Retained Earnings:
  $$\Delta \text{RetainedEarnings} = \sum \text{IncomeBalances} - \sum \text{ExpenseBalances}$$
  1. All Income balances are debited to 0.
  2. All Expense balances are credited to 0.
  3. Net Profit / Loss is transferred to `Retained Earnings (Equity)`.

---

## 3. Gherkin Functional Scenarios

### Scenario AC-01: Post Balanced Journal Entry
- **Given** an authorized accountant in Company `US-01`
- **When** the user submits a Journal Entry with:
  - Line 1: Debit `5110 - Office Supplies Expense` for $350.00
  - Line 2: Credit `1110 - Cash and Cash Equivalents` for $350.00
- **Then** status becomes `Submitted`
- **And** two balanced records are appended to `GLEntry`
- **And** the net difference is exactly $0.00.

### Scenario AC-02: Reject Imbalanced Voucher Submission
- **Given** a draft Journal Entry with total debits $1,000.00 and total credits $995.00
- **When** the user attempts to submit the voucher
- **Then** the submission is blocked with `DoubleEntryImbalanceException`
- **And** zero records are written to `GLEntry`.

### Scenario AC-03: Reject Posting to Group Account
- **Given** an account `1000 - Assets` with `IsGroup = true`
- **When** a voucher line references account `1000 - Assets`
- **Then** the transaction is rejected with error `PostingToGroupAccountProhibited`.

### Scenario AC-04: Freeze Date Lock Enforcement
- **Given** a company freeze date set to `2025-12-31`
- **When** an invoice or adjustment attempts to post on `2025-12-15`
- **Then** the command is rejected with `FiscalPeriodLockedException`
- **And** no data is modified.

### Scenario AC-05: Period Closing Voucher (Year-End Close)
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** _(superseded — see below)_ unimplemented at that date.
> **IMPLEMENTED 2026-10-08 by R-13** (archived `.specify/modules/archive/2026-10-08-13-fiscal-closing/`): covered by Domain fixtures (profit 500k/380k→Cr retained 120k, loss, break-even), 30 Domain + 18 handler tests, and live-submit acceptance in `verify-report.md`. Back in certified scope.

- **Given** Fiscal Year 2025 with total Revenue $500,000 and total Expenses $380,000 (Net Profit $120,000)
- **When** the `PeriodClosingVoucher` is posted on `2025-12-31`
- **Then** all Income accounts are debited to zero
- **And** all Expense accounts are credited to zero
- **And** `3100 - Retained Earnings` is credited for $120,000.00.

### Scenario AC-06: Idempotent Submission Guard
- **Given** a valid Journal Entry submission with header `Idempotency-Key: idemp-jv-2026-009`
- **When** network disruption causes the client to retry the identical POST command
- **Then** the system detects the processed idempotency key in cache/store
- **And** returns HTTP 200 with the previously created voucher details
- **And** strictly prevents duplicate ledger postings in `GLEntry`.

### Scenario AC-07: Cancellation & Reversal of Submitted Voucher
- **Given** a submitted voucher `JV-2026-0081` with balanced postings in `GLEntry`
- **When** the accountant submits a cancellation command
- **Then** the original voucher status transitions to `Cancelled`
- **And** compensating reversing `GLEntry` lines are posted (swapping original debits and credits)
- **And** historical audit records are preserved without in-place mutation.

