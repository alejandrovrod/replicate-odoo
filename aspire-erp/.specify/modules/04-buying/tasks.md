# Implementation Tasks: Buying & Procurement (ERPNext Parity)

**Module:** `04-buying`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** READY TO IMPLEMENT  

---

## Phase 4: Buying & Purchasing Engine

- [x] **Task 4.1: Supplier Master Entity & Accounts Mapping**
  - **Action:** Create `Supplier` entity in `Erp.Domain.Buying` with default payable account, tax ID, and payment terms.
  - **Acceptance:** Validates supplier codes and currency settings.

- [x] **Task 4.2: Purchase Order Workflow**
  - **Action:** Implement `PurchaseOrder` and `PurchaseOrderItem` with status tracking (PartiallyReceived, Completed).
  - **Acceptance:** Cannot alter line items once submitted.

- [x] **Task 4.3: Goods Receipt & Interim Accrual (`PurchaseReceipt`)**
  - **Action:** Submitting `PurchaseReceipt` increments warehouse physical stock and posts interim liability `Stock Received But Not Billed`.
  - **Acceptance:** `GLEntry` debits Stock In Hand and credits Stock Received But Not Billed.

- [x] **Task 4.4: Purchase Invoicing & 3-Way Match Validation**
  - **Action:** Submitting `PurchaseInvoice` validates received quantities, clears interim accrual, and books Accounts Payable.
  - **Acceptance:** Rejects invoices exceeding received quantity with `OverbillingNotAllowedException`.

- [x] **Task 4.5: React Procurement Studio UI**
  - **Action:** Build `BuyingOverview.tsx` in `erp-client` with purchase order status list, receipt approval modals, and vendor aging cards.
  - **Acceptance:** Real-time visibility into unbilled receipts.

- [x] **Task 4.6: Idempotent Submission & Reversal Handlers**
  - **Action:** Implement `Idempotency-Key` pipeline and `CancelPurchaseInvoiceCommandHandler` booking reversing `GLEntry` records.
  - **Acceptance:** Replaying an identical bill returns cached response; cancellation zeroes Accounts Payable cleanly.

- [x] **Task 4.7: 3-Way Matching Concurrency & Integration Tests**
  - **Action:** Write integration tests verifying concurrent billing against single receipt and asserting overbilling prevention.
  - **Acceptance:** 100% of overbilling edge cases are blocked with `OverbillingNotAllowedException`.

