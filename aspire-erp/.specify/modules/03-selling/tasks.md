# Implementation Tasks: Selling & Point of Sale (ERPNext Parity)

**Module:** `03-selling`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** READY TO IMPLEMENT  

---

## Phase 5: Selling, Invoicing & Point of Sale (POS)

- [ ] **Task 5.1: Customer Domain Model & Credit Limit Controls**
  - **Action:** Implement `Customer` entity in `Erp.Domain.Selling` with `CreditLimit`, `BypassCreditLimitCheck`, and outstanding balance tracking.
  - **Acceptance:** Credit calculation tests verify that breaches throw `CreditLimitExceededException`.

- [ ] **Task 5.2: Sales Order & Commitment Fulfillment**
  - **Action:** Implement `SalesOrder` aggregate root with line items, delivery date tracking, and fulfillment percentages.
  - **Acceptance:** Order status updates accurately upon partial and full shipments.

- [ ] **Task 5.3: Sales Invoice Posting & General Ledger Integration**
  - **Action:** Submitting `SalesInvoice` verifies customer credit limit and posts balanced `GLEntry` records (Debit A/R, Credit Sales Revenue, Credit Taxes).
  - **Acceptance:** Ledger balances balance to $0.00; updates customer outstanding debt.

- [ ] **Task 5.4: Atomic POS Register Checkout (`SubmitPOSInvoiceCommand`)**
  - **Action:** Implement retail POS checkout creating invoice, immediate cash/card payment entries, and inventory relief in a single atomic transaction.
  - **Acceptance:** Generates invoice in `Paid` status with $0.00 outstanding balance.

- [ ] **Task 5.5: React Sales Studio & POS Cashier UI**
  - **Action:** Build `SellingOverview.tsx` and retail checkout modal in `erp-client` with live discount/tax calculations and tender buttons.
  - **Acceptance:** Cashiers can scan products and submit sales in one click.
