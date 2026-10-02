# Functional Specification: Stock & Inventory (ERPNext Parity)

**Module:** `02-stock`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext Perpetual Inventory & Stock](https://docs.frappe.io/erpnext/stock)  

---

## 1. Executive Summary & Ubiquitous Language

The **Stock Module** manages physical inventory across hierarchical warehouses and maintains real-time financial valuation through **Perpetual Inventory Accounting**. Every physical material receipt, issue, transfer, or sale delivery immediately updates the physical quantity in the **Stock Ledger** and creates corresponding double-entry records in the **General Ledger**.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Item** | `Item` | A distinct product or SKU tracked in inventory with a primary Unit of Measure (UOM) and valuation method (default: FIFO). |
| **Warehouse** | `Warehouse` | Hierarchical physical or transit storage location linked directly to a Stock Asset Account in the Chart of Accounts. |
| **Unit of Measure (UOM)** | `UOM` & `UOM Conversion Detail` | Measuring unit (e.g. Unit, Box, Kg) with conversion factors to the item's default stock UOM. |
| **Stock Entry** | `Stock Entry` | Transaction document recording physical material movement: Material Receipt, Material Issue, Material Transfer, or Manufacture. |
| **Stock Ledger Entry (SLE)** | `Stock Ledger Entry` | Atomic, immutable ledger entry recording quantity movement, valuation rate, and stock balance per item and warehouse. |
| **Perpetual Inventory** | Perpetual Inventory | Automatic synchronization between physical inventory value ($\text{SLE.StockValue}$) and financial inventory accounts ($\text{GL.WarehouseAccount}$). |
| **FIFO Cost Queue** | FIFO Valuation | Layered valuation queue where inventory is relieved at the cost of the oldest unconsumed incoming batches ($[Qty_i, Rate_i]$). |
| **Stock Received But Not Billed** | Interim Liability | Accrual liability account credited upon physical goods receipt before vendor invoice receipt. |
| **Cost of Goods Sold (COGS)** | COGS Expense | Operating expense account debited when items are delivered to fulfill customer sales orders. |

---

## 2. Core Business Invariants & Valuation Rules

### Invariant ST-01: Perpetual Inventory Balance Synchronization
- For every stock movement that alters valuation:
  $$\Delta \text{StockValue} == \Delta \text{GLEntry}(\text{Warehouse.AccountId})$$
- The total balance of stock asset accounts in the General Ledger must match the sum of closing values across all active items in the Stock Ledger.

### Invariant ST-02: FIFO Costing Queue
- Inbound movements (Receipts) enqueue batches of $[Qty, Rate]$ into the item-warehouse FIFO queue.
- Outbound movements (Deliveries, Issues) dequeue batches starting from the oldest available layer until the required quantity is satisfied:
  $$\text{COGS} = \sum_{i=1}^{k} \text{ConsumedQty}_i \times \text{BatchRate}_i$$

### Invariant ST-03: Negative Stock Prohibition
- Physical stock quantity cannot drop below zero ($Qty < 0.0000$).
- If an outbound movement exceeds available on-hand inventory, the transaction is rejected with `InsufficientStockException`.

### Invariant ST-04: Material Transfer Quantity Conservation
- For any `MaterialTransfer` between warehouses:
  $$\sum \Delta Qty_{\text{Target}} - \sum \Delta Qty_{\text{Source}} == 0.0000$$

---

## 3. Gherkin Functional Scenarios

### Scenario ST-01: Purchase Receipt with Perpetual Inventory Accrual
- **Given** an approved Purchase Order for 50 units of `ITEM-STEEL-10` @ $20.00/unit
- **When** the warehouse manager submits a `PurchaseReceipt` into `Stores - Main`
- **Then** a `StockLedgerEntry` is recorded with $+50$ units @ $20.00 (Stock Value: $1,000.00)
- **And** balanced entries are posted to `GLEntry`:
  - Debit: `1310 - Stock in Hand (Stores)` for $1,000.00
  - Credit: `2120 - Stock Received But Not Billed` for $1,000.00.

### Scenario ST-02: Delivery Note with Multi-Layer FIFO Consumption
- **Given** item `ITEM-WIDGET-01` in `Stores - Main` with two inventory batches:
  - Batch 1: 30 units @ $10.00 ($300.00)
  - Batch 2: 30 units @ $15.00 ($450.00)
- **When** a `DeliveryNote` is posted for 40 units
- **Then** the FIFO engine consumes 30 units @ $10.00 ($300.00) + 10 units @ $15.00 ($150.00) = $450.00 total COGS
- **And** `GLEntry` records:
  - Debit: `5120 - Cost of Goods Sold` for $450.00
  - Credit: `1310 - Stock in Hand (Stores)` for $450.00
- **And** remaining stock balance is 20 units valued at $15.00/unit ($300.00).

### Scenario ST-03: Reject Stock Issue on Insufficient Stock
- **Given** item `ITEM-BEARING-02` with an on-hand balance of 5 units
- **When** a user attempts to issue 8 units on a `StockEntry`
- **Then** the command fails with `InsufficientStockException("Available: 5, Requested: 8")`
- **And** zero records are written to either `StockLedgerEntry` or `GLEntry`.
