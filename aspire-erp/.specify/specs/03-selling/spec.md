# Functional Specification: Selling & Point of Sale (ERPNext Parity)

**Module:** `03-selling`  
**Status:** CERTIFIED — scope amended 2026-10-04 after retro-verification (SL-01 invoice clause, invariants SL-01/SL-03 and scenarios SL-03..SL-06 marked DEFERRED inline)  
**Version:** 2.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext Selling & POS](https://docs.frappe.io/erpnext/selling)  

---

## 1. Executive Summary & Ubiquitous Language

The **Selling Module** manages commercial relationships with Customers, quotations, sales order commitments, fulfillment delivery notes, revenue invoicing, and high-speed **Point of Sale (POS)** retail checkout sessions. It coordinates with the **Stock Module** (for perpetual inventory delivery) and the **Accounting Module** (for automated Accounts Receivable and revenue recognition).

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Customer** | `Customer` | Commercial buyer entity holding credit terms, tax ID, currency, and default Accounts Receivable account. |
| **Quotation** | `Quotation` | Formal commercial offer stating item prices, discounts, and validity dates without creating financial or inventory commitments. |
| **Sales Order (SO)** | `Sales Order` | Confirmed customer purchase commitment that reserves inventory allocations and authorizes fulfillment. |
| **Delivery Note (DN)** | `Delivery Note` | Physical shipment document relieving inventory from warehouses and booking Cost of Goods Sold (COGS). |
| **Sales Invoice (SINV)** | `Sales Invoice` | Financial claim against the customer recognizing Sales Revenue, booking Taxes Payable, and establishing Accounts Receivable. |
| **Point of Sale (POS)** | `POS Invoice` & `POS Profile` | Rapid cashier checkout combining invoice creation, multi-mode payment collection, and warehouse stock deduction in a single atomic transaction. |
| **Credit Limit** | `Customer.credit_limit` | Maximum allowed exposure ($\text{Outstanding Debt} + \text{Unbilled Delivery} + \text{Pending Order}$). |
| **Update Stock** | `SalesInvoice.update_stock = 1` | Invoicing option that directly relieves inventory without requiring an intermediate Delivery Note. |

---

## 2. Core Business Invariants & Commercial Rules

### Invariant SL-01: Double-Entry Revenue Invariant
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** the sales-invoice posting path this equation describes is not reachable — `CreateSalesInvoiceCommandHandler`/`SubmitSalesInvoiceCommandHandler` are DI-registered but routed to no endpoint, `TaxTotal` is forced to `0`, and no test asserts any sales-invoice GL row. Out of the certified scope of this module; tracked as a carry-forward flag in this module's archive report.

Upon posting a standard `SalesInvoice`:
$$\text{Debit: Accounts Receivable} = \text{GrandTotal}$$
$$\text{Credit: Sales Revenue Account} = \text{NetTotal}$$
$$\text{Credit: Taxes Payable Account} = \text{TaxTotal}$$
$$\text{Debit} - \sum \text{Credit} == 0.0000$$

### Invariant SL-02: Credit Limit Protection
Before submitting a `SalesOrder` or credit `SalesInvoice`:
$$\text{Customer.OutstandingDebt} + \text{GrandTotal} \le \text{Customer.CreditLimit}$$
- If breached and `Customer.BypassCreditLimitCheck == false`, the command is rejected with `CreditLimitExceededException`.

> **SCOPE AMENDMENT 2026-10-04 (retro-verify):** the `SalesOrder` path of this invariant is certified (evaluator unit tests + API-level 409 coverage). The `credit SalesInvoice` clause is **DEFERRED** together with the sales-invoice posting path (see SL-01): no reachable code updates `Customer.OutstandingAmount`, and no test exercises cumulative exposure through an invoice.

### Invariant SL-03: Atomic POS Checkout
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** the POS path cannot run end to end as shipped — the cashier modal posts an unseeded hard-coded customer/profile and tenders a tax-inclusive amount against a server that forces `TaxTotal = 0`, the checkout books no COGS/stock pair, and zero tests cover `POST /api/v1/sales-invoices/pos`. Out of the certified scope of this module; tracked as a carry-forward flag in this module's archive report.

When `IsPOS == true`:
1. `SalesInvoice` is created in `Paid` status with `OutstandingAmount == 0.00`.
2. Immediate payment vouchers debit `Cash In Drawer` or `Card Clearing Account` for $\text{PaidAmount} == \text{GrandTotal}$.
3. If `UpdateStock == true`, inventory is relieved directly from the store warehouse and COGS is debited in `GLEntry`.
4. The entire operation executes in a single database transaction.

### Invariant SL-04: Non-Overdelivery Guard
A `DeliveryNote` or direct `SalesInvoice` cannot fulfill more than the remaining unfulfilled quantity on the referenced `SalesOrder`:
$$\text{DeliveredQty} \le \text{SalesOrderItem.Quantity} - \text{SalesOrderItem.DeliveredQty}$$

---

## 3. Gherkin Functional Scenarios

### Scenario SL-01: Standard Order-to-Cash Cycle
- **Given** an approved `SalesOrder` for 10 units of `ITEM-A` @ $100.00 ($1,000.00 + $100.00 Tax)
- **When** the fulfillment team submits a `DeliveryNote` for 10 units
- **Then** inventory is relieved from `Stores` and COGS is booked
- **When** the accounting team posts the `SalesInvoice`
- **Then** `GLEntry` debits `Accounts Receivable` for $1,100.00, credits `Sales Revenue` for $1,000.00, and credits `Tax Payable` for $100.00
- **And** the Customer's outstanding debt increases by $1,100.00.

> **SCOPE AMENDMENT 2026-10-04 (retro-verify):** only the `DeliveryNote` half of this cycle is certified (relief + COGS, tested at unit and API level). The `SalesInvoice` half — the A/R / revenue / tax posting and the debt increase — is **DEFERRED** with invariant SL-01: that code is unrouted and untested.

### Scenario SL-02: Reject Invoice on Credit Limit Breach
- **Given** Customer `ACME Corp` with Credit Limit $5,000.00 and existing outstanding balance $4,600.00
- **When** a user attempts to submit a new credit invoice for $650.00
- **Then** total exposure ($5,250.00) exceeds $5,000.00
- **And** the submission is blocked with `CreditLimitExceededException("Credit limit $5,000 exceeded. Current: $4,600, Attempted: $650")`.

### Scenario SL-03: High-Speed POS Multi-Tender Checkout
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** unimplemented end to end and untested (see invariant SL-03). Out of the certified scope of this module; tracked as a carry-forward flag in this module's archive report.

- **Given** an active retail POS register session
- **When** the cashier scans items totaling $85.00 ($80.00 net + $5.00 tax)
- **And** tenders payment: $50.00 in Cash and $35.00 via Credit Card
- **Then** `SalesInvoice` is submitted with `Status = Paid` and `IsPOS = true`
- **And** `GLEntry` records:
  - Debit: `1111 - Cash in Drawer` ($50.00)
  - Debit: `1115 - POS Card Clearing` ($35.00)
  - Credit: `4110 - Retail Sales Revenue` ($80.00)
  - Credit: `2210 - Sales Tax Payable` ($5.00)
- **And** store warehouse physical stock is decremented immediately.

### Scenario SL-04: Idempotent Sales Invoice Submission Guard
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** no reachable sales-invoice mutation carries `[IdempotencyKeyRequired]` (the only sales-invoice route, `POST .../pos`, is unguarded) and no test replays a sales-invoice submission. Out of the certified scope of this module; tracked as a carry-forward flag in this module's archive report.

- **Given** a valid `SalesInvoice` submission with header `Idempotency-Key: idemp-sinv-2026-44`
- **When** network retry triggers duplicate submission from the client
- **Then** the idempotency filter catches the existing transaction key
- **And** returns HTTP 200 with the original processed invoice DTO
- **And** strictly prevents duplicate receivables or double revenue recognition.

### Scenario SL-05: Cancellation & Credit Note Return
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** no `CancelSalesInvoice` handler, endpoint or test exists; `SalesInvoiceStatus.Cancelled` is never assigned and `UpdateStock` is hard-coded `false`, so the stock-return clause has no code path. Out of the certified scope of this module; tracked as a carry-forward flag in this module's archive report.

- **Given** a submitted `SalesInvoice` `SINV-2026-0012` for $1,100.00
- **When** the customer returns the order and a Credit Note is issued
- **Then** the invoice status transitions to `Cancelled`
- **And** reversing `GLEntry` records are posted (Debit Revenue/Tax, Credit Accounts Receivable)
- **And** if `UpdateStock == true`, inventory is returned to the warehouse via reversing `StockLedgerEntry`.

### Scenario SL-06: Concurrency Guard on Credit Limit & Stock Fulfillments
> **DEFERRED — scope amendment 2026-10-04 (retro-verify):** untested — no concurrent-credit-invoice test exists and a `RowVersion` conflict maps to `server_error` instead of `CreditLimitExceededException`. Related carry-forward safety flag: `SalesPostingService.PostDeliveryNoteAsync` reads FIFO layers WITHOUT the stock range lock that `StockPostingService` takes, so concurrent delivery notes on one item/warehouse can still oversell (no selling scenario exercises that race today). Out of the certified scope of this module; tracked as carry-forward flags in this module's archive report.

- **Given** Customer `ACME` with available credit $500.00
- **When** two branch users attempt to issue separate invoices for $400.00 concurrently
- **Then** optimistic concurrency locks verify the total pending balance
- **And** exactly one transaction succeeds; the second is rejected with `CreditLimitExceededException`.

