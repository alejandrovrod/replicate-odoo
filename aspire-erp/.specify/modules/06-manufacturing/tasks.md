# Implementation Tasks: Manufacturing & Production (ERPNext Parity)

**Module:** `06-manufacturing`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** READY TO IMPLEMENT  

---

## Phase 9: Manufacturing, BOM & Shop Floor Execution

- [ ] **Task 9.1: Workstation & Machine Center Entities**
  - **Action:** Create `Workstation` entity in `Erp.Domain.Manufacturing` with labor, electricity, and rent hourly rates.
  - **Acceptance:** Calculates composite hourly operating cost automatically.

- [ ] **Task 9.2: Bill of Materials (BOM) Aggregate & Cost Roll-Up**
  - **Action:** Implement `BOM`, `BOMItem`, and `BOMOperation` with automated cost calculation (raw materials + operations - scrap).
  - **Acceptance:** Validates that finished item cannot be a component of itself (anti-cycle check).

- [ ] **Task 9.3: Work Order Scheduling & Material Reservation**
  - **Action:** Create `WorkOrder` aggregate root with planned dates, source raw material warehouse, and WIP transit warehouse.
  - **Acceptance:** Submitting work order verifies that referenced BOM is active and default.

- [ ] **Task 9.4: Stock Movements for Manufacturing (WIP & Finish)**
  - **Action:** Implement material transfer to WIP and manufacture completion stock entries with automated cost capitalization into `GLEntry`.
  - **Acceptance:** Relieves components from WIP, credits operations absorption account, and debits finished goods stock.

- [ ] **Task 9.5: React Manufacturing & BOM Studio UI**
  - **Action:** Build BOM tree editor and Work Order execution board in `erp-client`.
  - **Acceptance:** Real-time visibility into production status and shop floor execution.
