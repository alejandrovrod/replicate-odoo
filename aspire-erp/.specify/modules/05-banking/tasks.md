# Implementation Tasks: Banking & Reconciliation (ERPNext Parity)

**Module:** `05-banking`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** READY TO IMPLEMENT  

---

## Phase 6: Treasury, Payments & Banking Subsystem

- [ ] **Task 6.1: Domain Models `PaymentEntry` and `PaymentAllocation`**
  - **Action:** Create `PaymentEntry` (PaymentType: Receive/Pay, PaidAmount, BankAccount) and `PaymentAllocation`.
  - **Acceptance:** Validates anti-overpayment invariant (`AllocatedAmount <= Invoice.OutstandingAmount`).

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
