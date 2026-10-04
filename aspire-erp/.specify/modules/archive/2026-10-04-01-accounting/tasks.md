# Implementation Tasks: Accounting & General Ledger (ERPNext Parity)

**Module:** `01-accounting`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** CERTIFIED — amended scope 2026-10-04 (11/11 tasks verified; FX + period-closing DEFERRED)  

---

## Phase 1: Chart of Accounts & Hierarchy Engine

- [x] **Task 1.1: Multi-Tenant `Account` Domain Entity & Tree Invariants**
  - **Action:** Create `Account` entity in `Erp.Domain` with properties (`AccountCode`, `AccountName`, `RootType`, `Type`, `IsGroup`, `ParentAccountId`, `Currency`, `IsActive`).
  - **Invariants:** Root accounts must have `ParentAccountId == null`; non-group accounts cannot have children; cyclical parent references are blocked.
  - **Acceptance:** Domain unit tests validate hierarchy constraints and entity invariants.

- [x] **Task 1.2: Hierarchy Query & Chart of Accounts CQRS Read Model**
  - **Action:** Implement `GetAccountTreeQuery` in `Erp.Application` resolving hierarchical account trees into structured DTOs with depth tracking.
  - **Acceptance:** Returns hierarchical JSON tree with O(N) traversal.

- [x] **Task 1.3: API Controller for Chart of Accounts**
  - **Action:** Expose `GET /api/v1/accounts/tree` and `POST /api/v1/accounts` in `AccountsController` with tenant resolution middleware.
  - **Acceptance:** HTTP integration tests return 200 OK with tenant-scoped account tree.

- [x] **Task 1.4: React Chart of Accounts Tree Table UI**
  - **Action:** Implement `AccountTreeTable.tsx` in `erp-client` using Radix UI and Tailwind CSS with expand/collapse, search filter, and root-type badges.
  - **Acceptance:** Renders accounts hierarchy with smooth indentation and sub-tree toggling.

---

## Phase 2: General Ledger Engine (`GLEntry`) & Journal Vouchers

- [x] **Task 2.1: Domain Model `GLEntry` & Invariant Enforcement**
  - **Action:** Implement immutable `GLEntry` entity enforcing non-negative debits/credits and zero-sum voucher balance ($\sum D - \sum C == 0.0000$).
  - **Acceptance:** `DoubleEntryImbalanceException` thrown when $\left|\sum D - \sum C\right| > 0.0001$.

- [x] **Task 2.2: Hard Fiscal Period Lock & Freeze Date**
  - **Action:** In `Company` entity, enforce `FrozenAccountsDate`. Any attempt to post a voucher where `PostingDate <= FrozenAccountsDate` must throw `FiscalPeriodLockedException`.
  - **Acceptance:** Back-dated voucher attempts in closed periods fail deterministically.

- [x] **Task 2.3: `JournalEntry` Aggregate & Posting Pipeline**
  - **Action:** Implement `JournalEntry` aggregate root in `Erp.Domain` supporting multi-line debits and credits, submission into `GLEntry`, and counter-reversals.
  - **Acceptance:** Submitting a journal entry appends balanced lines to `GLEntry`; group account lines throw `InvalidPostingAccountException`.

- [x] **Task 2.4: Journal Entries API Endpoints**
  - **Action:** Create `JournalEntriesController` in `Erp.Api` exposing `POST /api/v1/journal-entries`, `POST /api/v1/journal-entries/{id}/submit`, and `POST /api/v1/journal-entries/{id}/cancel`.
  - **Acceptance:** Returns 201 Created and updates ledger balances atomically.

- [x] **Task 2.5: Financial Reporting Queries (Trial Balance, Balance Sheet, P&L)**
  - **Action:** Implement CQRS queries:
    - `GetTrialBalanceQuery`: Validates total debits equal total credits.
    - `GetBalanceSheetQuery`: Assets = Liabilities + Equity.
    - `GetProfitAndLossQuery`: Revenue - COGS - Expenses = Net Profit.
  - **Acceptance:** Trial balance reports zero discrepancy.

- [x] **Task 2.6: React General Ledger Audit Viewer UI**
  - **Action:** Build `GeneralLedgerOverview.tsx` in `erp-client` with voucher drill-down, account filtering, and debit/credit total validation.
  - **Acceptance:** Table renders chronological audit entries with verified balance badge.

- [x] **Task 2.7: Idempotency-Key Handling (AC-06)**
  - **Action:** Integrate Idempotency-Key validation in `Erp.Api` and `Erp.Domain` to prevent duplicate submissions on retries.
  - **Acceptance:** Repeated submissions with the same idempotency key return the cached result without duplicating `GLEntry` records.
