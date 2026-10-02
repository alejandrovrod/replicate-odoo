# Functional Specification: Buying & Procurement (ERPNext Parity)

**Module:** `04-buying`  
**Status:** APPROVED  
**Version:** 1.0.0  
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
| **Tax Withholding (Retentions)** | Tax Withholding Category | Statutory deduction withheld at payment source, creating a direct tax authority liability while reducing net payable to the supplier. |

---

## 2. Core Business Invariants & Procurement Rules

### Invariant BY-01: Interim Liability Accrual on Goods Intake
When a `PurchaseReceipt` is submitted:
$$\text{Debit: Stock In Hand (Warehouse Account)} = \text{ReceivedQty} \times \text{ValuationRate}$$
$$\text{Credit: Stock Received But Not Billed (Interim Liability)} = \text{ReceivedQty} \times \text{ValuationRate}$$

### Invariant BY-02: Interim Liability Clearance & Payable Recognition
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
  - Debit: `1350 - Input VAT Recoverable` ($500.00)
  - Credit: `2110 - Accounts Payable (Creditors)` ($5,500.00)
- **And** the interim accrual is cleared ($0.00 balance).

### Scenario BY-03: Reject Overbilling on 3-Way Matching Breach
- **Given** a `PurchaseReceipt` with 10 units received
- **When** an accountant attempts to post a `PurchaseInvoice` referencing the receipt for 15 units
- **Then** the command is rejected with `OverbillingNotAllowedException("Cannot bill 15 units. Maximum receivable: 10")`
- **And** no ledger entries are created.
