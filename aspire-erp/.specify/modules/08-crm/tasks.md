# Implementation Tasks: CRM & Sales Pipeline (ERPNext Parity)

**Module:** `08-crm`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** IN PROGRESS (Block A: adopt + CRMActivity + EF + DI verified; 11.4/11.6/11.7 reopened pending Block B/C)  

---

## Phase 11: CRM, Leads & Opportunity Pipeline

- [x] **Task 11.1: Lead Domain Entity & Lifecycle**
  - **Action:** Implement `Lead` entity in `Erp.Domain.CRM` with qualification workflows and source attribution.
  - **Acceptance:** Validates email and telephone contact structures.

- [x] **Task 11.2: Commercial Opportunity & Weighted Forecasting**
  - **Action:** Implement `Opportunity` aggregate root with stage milestones and automated weighted revenue projection.
  - **Acceptance:** Pipeline forecaster tests confirm weighted math matches probability percentages.

- [x] **Task 11.3: Mandatory Loss Reason Validation**
  - **Action:** Enforce business invariant preventing deals from moving to `ClosedLost` without specifying a non-empty `LossReason`.
  - **Acceptance:** Null or whitespace loss reason throws `DomainValidationException`.

- [ ] **Task 11.4: Seamless Lead to Customer Conversion**
  > **Re-verification 2026-10-04 (adoptive Block A):** core conversion verified (Customer+Opportunity+Converted, green); audit-trail half unmet (no activity copy — CRMActivity entity added this block, wiring lands in Block B). Re-tick on Block B.
  - **Action:** Implement `ConvertLeadCommand` creating `Customer` and `Opportunity` while marking lead `Converted` and preserving timeline history.
  - **Acceptance:** Audit logs remain attached across the conversion boundary.

- [x] **Task 11.5: React Sales Funnel & Pipeline Board UI**
  - **Action:** Build Kanban opportunity pipeline board in `erp-client` with drag-and-drop stage updates.
  - **Acceptance:** Dragging opportunity across stages automatically recalculates weighted forecast banner.

- [ ] **Task 11.6: Idempotent Webhook Lead Intake & Re-opening Handlers**
  > **Re-verification 2026-10-04 (adoptive Block A):** re-open verified green; webhook idempotency unmet (IngestLead performs no dedup). Re-tick on Block B.
  - **Action:** Implement idempotency filter for marketing webhook ingestion and command handler for re-opening closed lost deals.
  - **Acceptance:** Replaying duplicate webhook payload returns cached result; re-opening lost deal sets stage to Negotiation cleanly.

- [ ] **Task 11.7: CRM Forecasting & Conversion Unit & Integration Tests**
  > **Re-verification 2026-10-04 (adoptive Block A):** unit side green; integration file does not exist yet. Re-tick on Block C.
  - **Action:** Write automated unit tests for weighted pipeline calculation and integration tests for lead-to-customer conversion.
  - **Acceptance:** 100% test pass on probability bounds, loss reason enforcement, and optimistic concurrency locks.

