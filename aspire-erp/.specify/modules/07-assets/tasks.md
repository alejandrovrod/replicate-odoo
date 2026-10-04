# Implementation Tasks: Asset Management & Depreciation (ERPNext Parity)

**Module:** `07-assets`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** IN PROGRESS (Block A: 10.1, 10.2, 10.3 complete)  

---

## Phase 10: Fixed Assets & Automated Depreciation

- [x] **Task 10.1: Asset Category & GL Account Templates**
  - **Action:** Create `AssetCategory` entity linking Fixed Asset, Accumulated Depreciation, and Expense accounts.
  - **Acceptance:** Validates that linked accounts are leaf posting accounts (`IsGroup == false`).

- [x] **Task 10.2: Asset Master & Capitalization**
  - **Action:** Implement `Asset` entity with purchase value, salvage value, and capitalization lifecycle.
  - **Acceptance:** Submitting capitalization generates initial balance sheet records.

- [x] **Task 10.3: Depreciation Schedule Generator**
  - **Action:** Implement `DepreciationScheduler` generating monthly straight-line schedule lines without rounding loss.
  - **Acceptance:** Invariant verified: $\sum \text{DepreciationSchedule.Amounts} == \text{GrossAmount} - \text{SalvageValue}$.

- [ ] **Task 10.4: Scheduled Periodic Depreciation Posting Background Worker**
  - **Action:** Implement background worker posting scheduled depreciation lines into `GLEntry` on their respective due dates.
  - **Acceptance:** Debits depreciation expense, credits accumulated depreciation, and marks schedule lines booked.

- [ ] **Task 10.5: Asset Disposal & Gain/Loss Balancing**
  - **Action:** Implement `DisposeAssetCommand` creating balanced ledger entries upon sale or scrap.
  - **Acceptance:** Clears asset cost and accumulated depreciation; books difference to gain/loss on disposal.

- [ ] **Task 10.6: Idempotent Depreciation Runner & Reversal Handlers**
  - **Action:** Implement idempotency filter for scheduled depreciation runs and reversal handler for erroneous asset disposals.
  - **Acceptance:** Multiple executions of monthly batch do not double-book depreciation expenses; reversal restores NBV.

- [ ] **Task 10.7: Depreciation Schedule & Disposal Unit & Integration Tests**
  - **Action:** Write automated unit tests for salvage value invariant verification and integration tests asserting balanced `GLEntry` on disposal.
  - **Acceptance:** 100% test pass on schedule generation rounding and disposal gain/loss calculations.

