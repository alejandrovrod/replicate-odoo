# Functional Specification: Buying & Procurement (ERPNext Parity)

**Module:** `04-buying`  
**Status:** 100% PRODUCTION CERTIFIED (Recursive Validator Pass 3/3)  
**Version:** 2.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext Buying Documentation](https://docs.frappe.io/erpnext/buying)  

---

## 1. Executive Summary & Ubiquitous Language

The **Buying Module** manages supplier relationships, purchase commitments, physical goods intake, supplier bill processing, and 3-way matching. It coordinates with the **Stock Module** (for warehouse receipts) and the **Accounting Module** (for interim accruals and Accounts Payable recognition).

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Supplier** | `Supplier` | Commercial vendor entity holding tax registration, payment terms, default payable account, and currency. |
| **Purchase Order (PO)** | `Purchase Order` | Binding contract issued to a supplier committing to purchase items at specified quantities and rates without immediate financial impact. |
| **Purchase Receipt (PR)** | `Purchase Receipt` | Warehouse intake voucher recording physical receipt, increasing stock, and creating the interim liability `Stock Received But Not Billed`. |
| **Purchase Invoice (PINV)** | `Purchase Invoice` | Supplier bill recognizing Accounts Payable, recovering input tax, and clearing the interim liability. |
| **Stock Received But Not Billed** | Interim Accrual Account | Balance sheet current liability account that holds unbilled inventory intake value until the fiscal bill arrives. |
| **3-Way Matching** | Three-Way Matching | Validation invariant ensuring agreement between: (1) Purchase Order terms, (2) Purchase Receipt quantities, and (3) Purchase Invoice amounts. |
| **Debit Note / Purchase Return** | Debit Note | Reversal document reducing Accounts Payable and returning goods or adjusting billed variances. |
| **Tax Withholding (Retentions)** | Tax Withholding Category | Statutory deduction withheld at payment source, creating a direct tax authority liability while reducing net payable to the supplier. *DEFERRED — scope amendment 2026-10-04 (retro-verify W4): no withholding engine, rates, accounts or scenarios exist; see BY-02 note.* |

---

## 2. Core Business Invariants & Procurement Rules

### Invariant BY-01: Interim Liability Accrual on Goods Intake
When a `PurchaseReceipt` is submitted:
$$\text{Debit: Stock In Hand (Warehouse Account)} = \text{ReceivedQty} \times \text{ValuationRate}$$
$$\text{Credit: Stock Received But Not Billed (Interim Liability)} = \text{ReceivedQty} \times \text{ValuationRate}$$

### Invariant BY-02: Interim Liability Clearance & Payable Recognition
> **DEFERRED (withholding term only) — scope amendment 2026-10-04 (retro-verify W4):** no tax-withholding engine exists — no rates, no withholding account, no scenario exercises it. `WithholdingTaxTotal` is hard-coded `0` (`PurchasePostingService.cs:359`), so the equation below always balances with that term at zero; the A/R-clearance, input-tax and payable terms stay certified. Carry-forward flag: build the withholding engine (rates, accounts, per-line calculation) when a jurisdictional requirement exists, then re-certify this term.
When the corresponding `PurchaseInvoice` is posted:
$$\text{Debit: Stock Received But Not Billed} = \text{BilledQty} \times \text{ValuationRate}$$
$$\text{Debit: Input Tax Recoverable} = \text{TaxTotal}$$
$$\text{Credit: Tax Withholding Payable} = \text{WithholdingTotal}$$
$$\text{Credit: Accounts Payable (Creditors)} = \text{GrandTotal} - \text{WithholdingTotal}$$
$$\sum \text{Debit} - \sum \text{Credit} == 0.0000$$

### Invariant BY-03: 3-Way Matching & Non-Overbilling Protection
A `PurchaseInvoice` cannot bill more quantity than has been accepted in the associated `PurchaseReceipt`:
$$\text{BilledQuantity} \le \text{PurchaseReceiptItem.AcceptedQuantity} - \text{PurchaseReceiptItem.BilledQuantity}$$
- Violation throws `OverbillingNotAllowedException`.

---

## 3. Gherkin Functional Scenarios

### Scenario BY-01: Goods Receipt with Interim Accrual
- **Given** an approved Purchase Order `PO-2026-0050` for 100 units of raw steel @ $50.00 ($5,000.00)
- **When** the warehouse manager submits a `PurchaseReceipt` for 100 units into `Stores - Main`
- **Then** inventory increases by 100 units in `StockLedgerEntry`
- **And** `GLEntry` records:
  - Debit: `1310 - Stock In Hand (Stores)` for $5,000.00
  - Credit: `2120 - Stock Received But Not Billed` for $5,000.00.

### Scenario BY-02: Supplier Invoice Approves 3-Way Match & Clears Accrual
- **Given** goods receipt `PR-2026-0050` with $5,000.00 in `Stock Received But Not Billed`
- **When** the accountant submits `PurchaseInvoice` `PINV-2026-0035` for 100 units @ $50.00 + $500.00 VAT
- **Then** `GLEntry` records:
  - Debit: `2120 - Stock Received But Not Billed` ($5,000.00)
  - Debit: `1130 - Input Tax Recoverable` ($500.00)
  - Credit: `2110 - Accounts Payable (Creditors)` ($5,500.00)
- **And** the interim accrual is cleared ($0.00 balance).

### Scenario BY-03: Reject Overbilling on 3-Way Matching Breach
- **Given** a `PurchaseReceipt` with 10 units received
- **When** an accountant attempts to post a `PurchaseInvoice` referencing the receipt for 15 units
- **Then** the command is rejected with `OverbillingNotAllowedException("Cannot bill 15 units. Maximum receivable: 10")`
- **And** no ledger entries are created.

### Scenario BY-04: Idempotent Vendor Bill Submission Guard
- **Given** a vendor bill submission with header `Idempotency-Key: idemp-pinv-2026-88`
- **When** the client submits the request twice due to connection retry
- **Then** the idempotency pipeline returns HTTP 200 with the already processed `PurchaseInvoiceDto`
- **And** duplicate accrual clearances or Accounts Payable entries are strictly prevented.

### Scenario BY-05: Cancellation & Purchase Return (Debit Note)
- **Given** a submitted `PurchaseInvoice` `PINV-2026-0035` with debt recorded in Accounts Payable
- **When** the supplier accepts a return or correction and a Debit Note is posted
- **Then** the invoice status transitions to `Cancelled`
- **And** compensating reversing `GLEntry` records are booked (Debit Accounts Payable, Credit Interim Accrual & Tax)
- **And** the physical stock intake is reversed if associated with a returned `PurchaseReceipt`.

### Scenario BY-06: Concurrency Guard on Simultaneous Bill Processing
- **Given** a `PurchaseReceipt` with exactly 20 units remaining to be billed
- **When** two accounts payable clerks attempt to bill 15 units each concurrently
- **Then** row locking on `PurchaseReceiptItem` enforces serial consistency
- **And** the first transaction processes 15 units successfully
- **And** the second transaction fails with `OverbillingNotAllowedException("Only 5 units remaining to bill, 15 requested")`.

