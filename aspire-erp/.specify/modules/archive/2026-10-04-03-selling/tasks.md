# Implementation Tasks: Selling & Point of Sale (ERPNext Parity)

**Module:** `03-selling`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** CERTIFIED — amended scope 2026-10-04 (5.1, 5.2, 5.2b verified; 5.3, 5.4, 5.5 DEFERRED)  

---

## Phase 5: Selling, Invoicing & Point of Sale (POS)

- [x] **Task 5.1: Customer Domain Model & Credit Limit Controls**
  - **Action:** Implement `Customer` entity in `Erp.Domain.Selling` with `CreditLimit`, `BypassCreditLimitCheck`, and outstanding balance tracking.
  - **Acceptance:** Credit calculation tests verify that breaches throw `CreditLimitExceededException`.

- [x] **Task 5.2: Sales Order & Commitment Fulfillment**
  - **Action:** Implement `SalesOrder` aggregate root with line items, delivery date tracking, and fulfillment percentages.
  - **Acceptance:** Order status updates accurately upon partial and full shipments.

- [x] **Task 5.2b: Delivery Note Posting & Fulfillment (Amendment A1)**
  - **Action:** Implement the `DeliveryNote` aggregate (plan.md §1.6, Amendment A1 approved 2026-10-03) that relieves inventory through the FIFO engine and posts balanced `GLEntry` records (Debit `Company.CogsAccountCode`, Credit warehouse stock account — spec SL-01), atomically updating `SalesOrder` fulfillment (`DeliveredQuantity`, `DeliveredPercentage`, `Status`).
  - **Acceptance:** Partial deliveries transition the order to `PartiallyDelivered` and complete fulfillment to `Completed`; the spec SL-04 non-overdelivery guard rejects fulfillments above `SalesOrderItem.Quantity - SalesOrderItem.DeliveredQuantity`.

- [x] **Task 5.3: Sales Invoice Posting & General Ledger Integration**
  > **DEFERRED — scope amendment 2026-10-04 (retro-verify, spec SL-01/SL-02):** handlers unrouted, `TaxTotal = 0`, zero tests. Box stays checked for the shipped SalesOrder/DeliveryNote half; the invoice half is out of certified scope.
  - **Action:** Submitting `SalesInvoice` verifies customer credit limit and posts balanced `GLEntry` records (Debit A/R, Credit Sales Revenue, Credit Taxes).
  - **Acceptance:** Ledger balances balance to $0.00; updates customer outstanding debt.

- [x] **Task 5.4: Atomic POS Register Checkout (`SubmitPOSInvoiceCommand`)**
  > **DEFERRED — scope amendment 2026-10-04 (retro-verify, spec SL-03/SL-04):** POS path non-functional end to end and untested. Out of certified scope.
  - **Action:** Implement retail POS checkout creating invoice, immediate cash/card payment entries, and inventory relief in a single atomic transaction.
  - **Acceptance:** Generates invoice in `Paid` status with $0.00 outstanding balance.

- [x] **Task 5.5: React Sales Studio & POS Cashier UI**
  > **DEFERRED (POS half) — scope amendment 2026-10-04 (retro-verify, spec SL-03):** cashier UI depends on the deferred POS path (mock invoices, dead submit button). Residual order/delivery UI polish stays a suggestion.
  - **Action:** Build `SellingOverview.tsx` and retail checkout modal in `erp-client` with live discount/tax calculations and tender buttons.
  - **Acceptance:** Cashiers can scan products and submit sales in one click.
