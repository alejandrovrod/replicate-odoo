# Implementation Tasks: CRM & Sales Pipeline (ERPNext Parity)

**Module:** `08-crm`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** READY TO IMPLEMENT  

---

## Phase 11: CRM, Leads & Opportunity Pipeline

- [ ] **Task 11.1: Lead Domain Entity & Lifecycle**
  - **Action:** Implement `Lead` entity in `Erp.Domain.CRM` with qualification workflows and source attribution.
  - **Acceptance:** Validates email and telephone contact structures.

- [ ] **Task 11.2: Commercial Opportunity & Weighted Forecasting**
  - **Action:** Implement `Opportunity` aggregate root with stage milestones and automated weighted revenue projection.
  - **Acceptance:** Pipeline forecaster tests confirm weighted math matches probability percentages.

- [ ] **Task 11.3: Mandatory Loss Reason Validation**
  - **Action:** Enforce business invariant preventing deals from moving to `ClosedLost` without specifying a non-empty `LossReason`.
  - **Acceptance:** Null or whitespace loss reason throws `DomainValidationException`.

- [ ] **Task 11.4: Seamless Lead to Customer Conversion**
  - **Action:** Implement `ConvertLeadCommand` creating `Customer` and `Opportunity` while marking lead `Converted` and preserving timeline history.
  - **Acceptance:** Audit logs remain attached across the conversion boundary.

- [ ] **Task 11.5: React Sales Funnel & Pipeline Board UI**
  - **Action:** Build Kanban opportunity pipeline board in `erp-client` with drag-and-drop stage updates.
  - **Acceptance:** Dragging opportunity across stages automatically recalculates weighted forecast banner.
