# Implementation Tasks: Manufacturing & Production (ERPNext Parity)

**Module:** `06-manufacturing`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** IMPLEMENTATION COMPLETE — all 7 tasks verified (pending Spec Kit verify + archive)  

---

## Phase 9: Manufacturing, BOM & Shop Floor Execution

- [x] **Task 9.1: Workstation & Machine Center Entities**
  - **Action:** Create `Workstation` entity in `Erp.Domain.Manufacturing` with labor, electricity, and rent hourly rates.
  - **Acceptance:** Calculates composite hourly operating cost automatically.

- [x] **Task 9.2: Bill of Materials (BOM) Aggregate & Cost Roll-Up**
  - **Action:** Implement `BOM`, `BOMItem`, and `BOMOperation` with automated cost calculation (raw materials + operations - scrap).
  - **Acceptance:** Validates that finished item cannot be a component of itself (anti-cycle check).

- [x] **Task 9.3: Work Order Scheduling & Material Reservation**
  - **Action:** Create `WorkOrder` aggregate root with planned dates, source raw material warehouse, and WIP transit warehouse.
  - **Acceptance:** Submitting work order verifies that referenced BOM is active and default.

- [x] **Task 9.4: Stock Movements for Manufacturing (WIP & Finish)**
  - **Action:** Implement material transfer to WIP and manufacture completion stock entries with automated cost capitalization into `GLEntry`.
  - **Acceptance:** Relieves components from WIP, credits operations absorption account, and debits finished goods stock.

- [x] **Task 9.5: React Manufacturing & BOM Studio UI**
  - **Action:** Build BOM tree editor and Work Order execution board in `erp-client`.
  - **Acceptance:** Real-time visibility into production status and shop floor execution.

- [x] **Task 9.6: Idempotent Manufacturing & Reversal Handlers**
  - **Action:** Implement `Idempotency-Key` pipeline for manufacture completion and cancellation handler for reversing material transfers.
  - **Acceptance:** Replaying duplicate completion returns cached result; cancelling work order reverses WIP stock back to stores.

- [x] **Task 9.7: BOM Recursion & Cost Roll-up Unit & Integration Tests**
  - **Action:** Write automated unit tests for BOM circular dependency prevention and integration tests verifying $\sum \text{Debit} == \sum \text{Credit}$ on manufacture.
  - **Acceptance:** 100% test pass on cost roll-up calculations, scrap deductions, and concurrency guards.

