# Functional Specification: Payment Entry & Receivable/Payable Settlement (ERPNext Parity)

**Module:** `r-12-payment-entry`  
**Status:** DRAFT — pending review (roadmap.md row **R-12**; formally depends on R-11, phased per §5)  
**Version:** 1.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** ERPNext `erpnext/accounts/doctype/payment_entry` — verified via `gitmcp.io/frappe/erpnext`

---

## 1. Executive Summary & Ubiquitous Language

The **Payment Entry** is the submittable voucher that settles open receivables (Sales Invoices) and payables (Purchase Invoices) against a liquid bank account. It closes the accounting loop the audited foundation left open: `PaymentEntry` / `PaymentAllocation` exist as domain entities (05-banking staging scope) but **no controller exposes them**, so A/R and A/P can never be settled through the API (roadmap.md §2 cross-cutting finding). This spec upgrades the banking voucher into the full ERPNext-parity document: party, lifecycle (Draft → Submitted → Cancelled), gapless fiscal numbering, General Ledger settlement, invoice outstanding tracking and advances.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Payment Entry** | `Payment Entry` | Submittable financial voucher recording money received from a Customer or paid to a Supplier through a Bank Account. Posts to the GL exactly once, on submit. |
| **Payment Type** | `payment_type` | Direction of the money: `Receive` (Dr bank / Cr receivable) or `Pay` (Dr payable / Cr bank). `Internal Transfer` is deferred (§6). |
| **Party** | `party_type` / `party` | Counterparty the voucher settles: a `Customer` for `Receive`, a `Supplier` for `Pay` (invariant PE-06). |
| **Payment Allocation** | `Payment Entry Reference` | One slice of the paid amount applied to a specific open invoice (Sales or Purchase reference, mutually exclusive). |
| **Paid Amount** | `paid_amount` | Total money moved through the bank; strictly positive (decimal(18,4)). |
| **Unallocated Amount (Advance)** | `unallocated_amount` | `PaidAmount − Σ allocations`: money received/paid without invoice backing (customer or supplier advance). Never negative. |
| **Outstanding Amount** | `outstanding_amount` | Invoice balance still owed: `GrandTotal − Σ submitted allocations`. Drives `Unpaid → PartiallyPaid → Paid`. |
| **Document Status** | `docstatus` | Voucher lifecycle: `Draft` (no GL impact) → `Submitted` (GL settled) → `Cancelled` (compensating reversal). |
| **Clearance Date** | `clearance_date` | Stamped by the Bank Reconciliation Tool (invariant BN-03) on the SUBMITTED voucher; never overwrites `PaymentDate`. |
| **Voucher Number** | `name` (naming series) | Gapless human number `PAY-<year>-<seq>` assigned inside the posting transaction per company & calendar year (Constitution III.4). |

---

## 2. Core Business Invariants & Payment Rules

### Invariant PE-01: Settlement Double-Entry Law
On submit, exactly one balanced voucher hits the General Ledger (Constitution III.1):
- `Receive`: **Dr** `BankAccount.GLAccountId` (PaidAmount) / **Cr** receivable leaf (PaidAmount)
- `Pay`: **Dr** payable leaf (PaidAmount) / **Cr** `BankAccount.GLAccountId` (PaidAmount)

$$\sum_{i=1}^{n} \text{Debit}_i - \sum_{i=1}^{n} \text{Credit}_i = 0.0000$$

Draft Payment Entries have zero GL impact: $\Delta \text{GLEntry}_{\text{Draft}} = 0$.

### Invariant PE-02: Anti-Overpayment Law
For every invoice $v$ referenced by the voucher (extends the existing `PaymentEntry.Allocate` guard to purchase bills):

$$\text{alreadyAllocated}_v + \text{amount} \;\le\; \text{OutstandingAmount}_v$$

A breach throws `over_allocation` and the whole posting transaction rolls back: zero rows persisted.

### Invariant PE-03: Payment Conservation Law
$$\text{PaidAmount} = \sum_{i=1}^{m} \text{AllocatedAmount}_i + \text{UnallocatedAmount}, \qquad \text{UnallocatedAmount} \ge 0$$

All allocation targets belong to the SAME Party, Company and Tenant as the voucher header.

### Invariant PE-04: Lifecycle & Gapless Numbering
Transitions allowed ONLY `Draft → Submitted → Cancelled`. `VoucherNo` is assigned when first submitted, inside the isolated numbering lock (Constitution III.4); a rolled-back attempt leaves no fiscal gap and an idempotent retry receives a fresh number.

### Invariant PE-05: Cancellation by Compensating Reversal
Cancelling a `Submitted` voucher NEVER mutates or deletes ledger rows (Constitution III.2/III.3): it appends mirrored lines (Debit/Credit swapped, `IsCancelled = true`, original `PostingDate`, same `VoucherNo`), restores each invoice's `OutstandingAmount`/`PaidAmount` and recomputes its status. A voucher whose `ClearanceDate` is set (reconciled, BN-03) CANNOT be cancelled until un-reconciled (BN-06).

### Invariant PE-06: Directional Consistency
$$\text{PaymentType} = \text{Receive} \Longleftrightarrow \text{PartyType} = \text{Customer}, \qquad \text{PaymentType} = \text{Pay} \Longleftrightarrow \text{PartyType} = \text{Supplier}$$

`Receive` allocations may only reference `SalesInvoice`; `Pay` only `PurchaseInvoice` — enforced by the application guard AND the `CK_PaymentAllocation_ExactlyOneInvoice` / `CK_PaymentEntry_Direction` CHECK constraints (defense in depth).

---

## 3. Gherkin Functional Scenarios

### Scenario PE-01: Full Customer Receipt Settles the Invoice (Happy Path)
- **Given** a submitted `SalesInvoice` `SINV-2026-00007` with `GrandTotal = 1,000.00` and `OutstandingAmount = 1,000.00`
- **And** customer receivable resolves `1210 - Accounts Receivable` and the voucher draws on `BankAccount` → `1110 - Main Bank`
- **When** the accountant submits a `Receive` Payment Entry of `1,000.00` allocated in full to the invoice
- **Then** `DocumentStatus` becomes `Submitted` with the gapless `VoucherNo` `PAY-2026-00001`
- **And** `GLEntry` gains Dr 1110 $1,000.00 / Cr 1210 $1,000.00 with `VoucherType = "PaymentEntry"`, `PartyType = "Customer"`
- **And** the invoice shows `PaidAmount = 1,000.00`, `OutstandingAmount = 0.00`, `Status = Paid`.

### Scenario PE-02: Partial Supplier Payment
- **Given** a posted `PurchaseInvoice` `PINV-2026-00042` with `GrandTotal = 5,500.00` and `OutstandingAmount = 5,500.00`
- **When** a `Pay` Payment Entry allocates `2,000.00` to it
- **Then** `GLEntry` gains Dr `2110 - Accounts Payable` $2,000.00 / Cr bank $2,000.00
- **And** the bill shows `OutstandingAmount = 3,500.00`, `Status = PartiallyPaid`.

### Scenario PE-03: Advance Payment Without Invoice (Unallocated)
- **Given** customer `ACME Corp` wires a retainer of `900.00` with no open invoice
- **When** the accountant submits a `Receive` entry with zero allocation rows
- **Then** the voucher is `Submitted` with `UnallocatedAmount = 900.00` and the GL settles Dr bank / Cr receivable in full
- **And** the advance remains recorded on the voucher for future allocation (consumption UI deferred, §6).

### Scenario PE-04: Overpayment Rejection Leaves Zero Side Effects
- **Given** an invoice with `OutstandingAmount = 1,000.00` and `800.00` already allocated by another submitted voucher
- **When** a new Payment Entry tries to allocate `250.00` to the same invoice
- **Then** the handler throws the `over_allocation` domain error
- **And** the API returns RFC 7807 ProblemDetails carrying the invariant `code = over_allocation`
- **And** no `GLEntry`, no `PaymentAllocation` and no invoice mutation is persisted (full transaction rollback proof).

### Scenario PE-05: Cancellation Writes the Compensating Reversal
- **Given** the submitted voucher `PAY-2026-00001` that fully paid `SINV-2026-00007` (scenario PE-01) and `ClearanceDate IS NULL`
- **When** the supervisor cancels the voucher
- **Then** `GLEntry` appends two mirrored rows (Cr 1110 / Dr 1210, `IsCancelled = true`) netting the voucher to exactly 0.0000 while the originals stay byte-identical
- **And** the invoice returns to `OutstandingAmount = 1,000.00`, `Status = Unpaid`
- **And** cancelling the same voucher again → `payment_already_cancelled`.

### Scenario PE-06: Idempotent Submit Replay
- **Given** a client that submits the same payment twice (double-click / network retry) with the same `Idempotency-Key` header
- **When** the second `POST .../submit` replays after the first one committed
- **Then** exactly ONE voucher and ONE balanced GL set exist and the second call returns the first call's recorded response
- **And** no duplicate voucher number is consumed (no fiscal gap, Constitution III.4).

### Scenario PE-07: Concurrent Payments Racing on the Same Invoice
- **Given** an invoice with `OutstandingAmount = 1,000.00`
- **When** two clerks simultaneously submit payments of `800.00` and `300.00` against it
- **Then** the invoice `RowVersion` compare-and-swap plus the fresh outstanding re-read inside the serializable posting lock lets exactly ONE commit
- **And** the loser receives `ConcurrencyConflictException` (409) or `over_allocation` after re-validation — never a negative outstanding balance.

### Scenario PE-08: Bank Reconciliation Compatibility (BN-03 Preserved)
- **Given** the submitted `Receive` voucher `PAY-2026-00009` for `1,000.00`
- **When** the Bank Reconciliation Tool matches it to an imported deposit line
- **Then** the voucher `Status` becomes `Reconciled` with `ClearanceDate = 2026-10-12` while `PaymentDate` and every original GL row stay untouched
- **And** attempting to cancel the voucher while reconciled → `payment_reconciled_cannot_cancel` until it is un-reconciled (BN-06).

---

## 4. API Contract (summary; full detail in plan.md §5)

| Method & Route | Purpose | Guards |
| :--- | :--- | :--- |
| `POST /api/v1/paymententries` | Create `Draft` voucher with its allocations | `TenantMember`, `IdempotencyKeyRequired` |
| `GET /api/v1/paymententries` | Paged list (party / type / document status / date range) | `TenantMember` |
| `GET /api/v1/paymententries/{id}` | Voucher detail with allocations and GL preview | `TenantMember` |
| `POST /api/v1/paymententries/{id}/submit` | Assign gapless number, post GL, settle invoices | `TenantMember`, `IdempotencyKeyRequired` |
| `POST /api/v1/paymententries/{id}/cancel` | Compensating reversal (requires `RowVersion`) | `TenantMember`, `IdempotencyKeyRequired` |
| `GET /api/v1/customers/{id}/outstanding-invoices` | Allocation source grid for `Receive` | `TenantMember` |
| `GET /api/v1/suppliers/{id}/outstanding-invoices` | Allocation source grid for `Pay` | `TenantMember` |

---

## 5. Dependency & Phasing (roadmap.md working agreement)

R-12 formally depends on **R-11 (Sales Invoice create/submit/cancel routed)**. The change is therefore phased:

- **Block A — Pay side** (`Supplier` / `PurchaseInvoice`): 100% executable today; the purchase pipeline (`PurchasePostingService`, gapless `VoucherNo`, cancel-with-reversal) is certified.
- **Block B — Receive side** (`Customer` / `SalesInvoice`): full domain and plan delivered in this change; the E2E suite runs against the existing (unrouted) `SubmitSalesInvoiceCommandHandler` by seeding a Submitted invoice, and formal certification re-runs after R-11 archives the real HTTP route.

---

## 6. Non-Goals / Explicitly Deferred

- **Multi-currency & FX gain/loss on payment** → R-14 (single-currency pending the revaluation module).
- **`Internal Transfer`** (bank-to-bank move) and the **Mode of Payment** catalog.
- **Write-off / deduction lines** on allocation (ERPNext `deductions` child table).
- **Tax withholding on payment** → R-15.
- **Payment Terms schedule** split allocations; **POS immediate-payment path** → R-20.
- **Re-routing the banking "Quick Voucher"** (BN-04) through this posting engine → recorded as a 05-banking carry-forward.
- **Advance consumption UI** (apply a previously unallocated balance to a later invoice) → follow-up change.
