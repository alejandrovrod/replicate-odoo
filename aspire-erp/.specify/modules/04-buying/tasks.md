# Implementation Tasks: Buying & Procurement (ERPNext Parity)

**Module:** `04-buying`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** READY TO IMPLEMENT  

---

## Phase 4: Buying & Purchasing Engine

- [ ] **Task 4.1: Supplier Master Entity & Accounts Mapping**
  - **Action:** Create `Supplier` entity in `Erp.Domain.Buying` with default payable account, tax ID, and payment terms.
  - **Acceptance:** Validates supplier codes and currency settings.

- [ ] **Task 4.2: Purchase Order Workflow**
  - **Action:** Implement `PurchaseOrder` and `PurchaseOrderItem` with status tracking (PartiallyReceived, Completed).
  - **Acceptance:** Cannot alter line items once submitted.

- [ ] **Task 4.3: Goods Receipt & Interim Accrual (`PurchaseReceipt`)**
  - **Action:** Submitting `PurchaseReceipt` increments warehouse physical stock and posts interim liability `Stock Received But Not Billed`.
  - **Acceptance:** `GLEntry` debits Stock In Hand and credits Stock Received But Not Billed.

- [ ] **Task 4.4: Purchase Invoicing & 3-Way Match Validation**
  - **Action:** Submitting `PurchaseInvoice` validates received quantities, clears interim accrual, and books Accounts Payable.
  - **Acceptance:** Rejects invoices exceeding received quantity with `OverbillingNotAllowedException`.

- [ ] **Task 4.5: React Procurement Studio UI**
  - **Action:** Build `BuyingOverview.tsx` in `erp-client` with purchase order status list, receipt approval modals, and vendor aging cards.
  - **Acceptance:** Real-time visibility into unbilled receipts.
