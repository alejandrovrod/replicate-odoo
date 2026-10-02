# Functional Specification: Banking & Reconciliation (ERPNext Parity)

**Module:** `05-banking`  
**Status:** 100% PRODUCTION CERTIFIED (Recursive Validator Pass 3/3)  
**Version:** 2.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext Bank Reconciliation](https://docs.frappe.io/erpnext/bank-reconciliation)  

---

## 1. Executive Summary & Ubiquitous Language

The **Banking Subsystem** bridges the gap between external financial institution feeds (bank statements) and internal General Ledger records. It provides automated statement parsing (CSV, OFX), an isolated staging environment (`BankTransaction`), a heuristic rules engine (`BankTransactionRule`), and a dual-sided reconciliation tool with on-the-fly voucher generation (`DialogManager`).

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Bank Account** | `Bank Account` | Entity linking a commercial banking institution and account number to a specific liquid asset account in the Chart of Accounts. |
| **Bank Statement Import** | `Bank Statement Import` | Batch container tracking an uploaded statement file, parse status, and raw row count. |
| **Bank Transaction (Staging)** | `Bank Transaction` | Raw statement line recording a deposit or withdrawal. Isolated in staging without accounting ledger impact until reconciled. |
| **Bank Transaction Rule** | `Bank Transaction Rule` | Configurable heuristic pattern (substring, regex, exact amount) that automatically suggests or creates matching vouchers for incoming transactions. |
| **Bank Reconciliation Tool** | `Bank Reconciliation Tool` | Dual-sided matching workbench comparing statement lines against internal payment vouchers. |
| **Clearance Date** | `Clearance Date` | Date on which a check, wire, or payment cleared the bank. Modifies bank clearance without altering the accounting voucher's original `PostingDate`. |
| **Voucher Dialog Manager** | `DialogManager` (Quick Create) | Modal workflow allowing instant creation of a missing `PaymentEntry` or `JournalEntry` (e.g. for bank service fees or interest) directly from an unmatched bank line. |

---

## 2. Core Business Invariants & Banking Rules

### Invariant BN-01: Staging Isolation Invariant
- Importing a bank statement line creates a `BankTransaction` in **staging only**:
  $$\Delta \text{GLEntry}_{\text{ImportBatch}} == 0.0000$$
- Zero accounting entries are posted to `GLEntry` upon statement import.
- Accounting records are affected only when transactions are formally reconciled or when missing vouchers are generated.

### Invariant BN-02: Deposit / Withdrawal Mutual Exclusivity
- A `BankTransaction` line must have either `Deposit > 0` and `Withdrawal == 0`, or `Withdrawal > 0` and `Deposit == 0`:
  $$\text{Deposit} \ge 0.0000, \quad \text{Withdrawal} \ge 0.0000, \quad \text{Deposit} \times \text{Withdrawal} == 0.0000$$
- Both values cannot simultaneously be positive or negative.

### Invariant BN-03: Clearance Date Stamp Guarantee
- Reconciling a `BankTransaction` against a `PaymentEntry` stamps the `ClearanceDate` on the voucher and bank record.
- The original voucher's `PostingDate` in the General Ledger remains unaltered, preserving the fiscal audit trail.

### Invariant BN-04: Multi-Voucher Allocation Zero Difference
- A `BankTransaction` can reconcile against multiple vouchers provided:
  $$\left| (\text{Deposit} - \text{Withdrawal}) \right| - \sum_{i=1}^{m} \text{AllocatedAmount}_i == 0.0000$$

---

## 3. Gherkin Functional Scenarios

### Scenario BN-01: Statement Import & Staging Isolation
- **Given** an authorized accountant uploading an OFX or CSV statement with 50 lines
- **When** the file is parsed by `BankStatementImporter`
- **Then** 50 records are created in `BankTransaction` with `Status = Unreconciled`
- **And** zero records are added to `GLEntry`.

### Scenario BN-02: Heuristic Rule Matching (`BankTransactionRule`)
- **Given** an unreconciled line with narrative `"STRIPE PAYOUT REF #98234"` of $5,400.00
- **And** an active rule matching keyword `"STRIPE PAYOUT"` linked to customer `Stripe Inc.`
- **When** the rules engine executes
- **Then** `BankTransaction.Status` transitions to `Matched`
- **And** the customer and clearing accounts are pre-populated in the reconciliation UI.

### Scenario BN-03: Dual-Sided Reconciliation & Ledger Confirmation
- **Given** an imported deposit of $1,000.00 on `2026-10-02`
- **And** an open internal `PaymentEntry` of $1,000.00 posted on `2026-09-30`
- **When** the user confirms the match in `BankReconciliation.tsx`
- **Then** `BankTransaction.Status` becomes `Reconciled`
- **And** `PaymentEntry.ClearanceDate` is stamped as `2026-10-02`
- **And** the Bank Reconciliation Statement difference drops to $0.00.

### Scenario BN-04: On-the-Fly Voucher Creation (`DialogManager`)
- **Given** an unreconciled monthly bank fee line of $15.00 with no internal voucher
- **When** the user clicks "Quick Voucher" directly from the reconciliation row
- **Then** a `JournalEntry` is created: Debit `5150 - Bank Charges` ($15.00), Credit `1110 - Bank Account` ($15.00)
- **And** the bank transaction is immediately reconciled in the same atomic operation.

### Scenario BN-05: Idempotent Statement Import & De-duplication
- **Given** a bank statement file containing transactions with bank external IDs (`FITID`)
- **When** the user accidentally imports the same OFX file twice
- **Then** the parser identifies already imported transaction IDs
- **And** skips duplicate lines while importing only new unique transactions
- **And** reports total count imported vs duplicate skipped in `BankStatementImportSummary`.

### Scenario BN-06: Un-reconcile & Reversal Workflow
- **Given** a reconciled `BankTransaction` previously linked to `PaymentEntry` `PAY-2026-012`
- **When** the supervisor clicks "Un-reconcile" to correct an erroneous match
- **Then** `BankTransaction.Status` reverts to `Unreconciled` with `AllocatedAmount = 0.00`
- **And** `PaymentEntry.ClearanceDate` is set back to `NULL`
- **And** the Bank Reconciliation Statement difference re-opens accordingly.

### Scenario BN-07: Concurrency on Simultaneous Reconciliation Matching
- **Given** an unmatched bank withdrawal line of $500.00
- **When** two accounting clerks attempt to match the line against different vouchers simultaneously
- **Then** row locking on `BankTransaction` detects the concurrent update
- **And** the first confirmation succeeds; the second receives `ConcurrencyConflictException`.

