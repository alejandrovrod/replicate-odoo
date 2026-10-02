# Implementation Tasks: Asset Management & Depreciation (ERPNext Parity)

**Module:** `07-assets`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** READY TO IMPLEMENT  

---

## Phase 10: Fixed Assets & Automated Depreciation

- [ ] **Task 10.1: Asset Category & GL Account Templates**
  - **Action:** Create `AssetCategory` entity linking Fixed Asset, Accumulated Depreciation, and Expense accounts.
  - **Acceptance:** Validates that linked accounts are leaf posting accounts (`IsGroup == false`).

- [ ] **Task 10.2: Asset Master & Capitalization**
  - **Action:** Implement `Asset` entity with purchase value, salvage value, and capitalization lifecycle.
  - **Acceptance:** Submitting capitalization generates initial balance sheet records.

- [ ] **Task 10.3: Depreciation Schedule Generator**
  - **Action:** Implement `DepreciationScheduler` generating monthly straight-line schedule lines without rounding loss.
  - **Acceptance:** Invariant verified: $\sum \text{DepreciationSchedule.Amounts} == \text{GrossAmount} - \text{SalvageValue}$.

- [ ] **Task 10.4: Scheduled Periodic Depreciation Posting Background Worker**
  - **Action:** Implement background worker posting scheduled depreciation lines into `GLEntry` on their respective due dates.
  - **Acceptance:** Debits depreciation expense, credits accumulated depreciation, and marks schedule lines booked.

- [ ] **Task 10.5: Asset Disposal & Gain/Loss Balancing**
  - **Action:** Implement `DisposeAssetCommand` creating balanced ledger entries upon sale or scrap.
  - **Acceptance:** Clears asset cost and accumulated depreciation; books difference to gain/loss on disposal.
