# Implementation Tasks: Stock & Perpetual Inventory (ERPNext Parity)

**Module:** `02-stock`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** IN PROGRESS  

---

## Phase 3: Multi-Warehouse Inventory & Kardex FIFO

- [ ] **Task 3.1: Domain Entities `Item`, `Warehouse`, and `UOM`**
  - **Action:** Implement `Item` (with valuation method FIFO), `Warehouse` (with account mapping), and `UOM` in `Erp.Domain.Stock`.
  - **Acceptance:** Validates that warehouse accounts resolve through hierarchy to company defaults.

- [ ] **Task 3.2: Stock Movements & `StockEntry` Aggregate**
  - **Action:** Create `StockEntry` supporting MaterialReceipt, MaterialIssue, and MaterialTransfer with item validation.
  - **Acceptance:** Cannot transfer materials to the identical warehouse; validates that item exists and is active.

- [ ] **Task 3.3: FIFO Costing Engine & `StockLedgerEntry` (SLE)**
  - **Action:** Implement `FifoCostEngine` and `StockLedgerEntry` recording running physical quantity and stock value.
  - **Acceptance:** Inbound receipts create new cost batches; outbound issues consume oldest available layers.

- [ ] **Task 3.4: Perpetual Inventory Accounting Integration**
  - **Action:** Submitting a `StockEntry` automatically books balanced `GLEntry` records linking warehouse accounts with COGS or Stock Adjustment accounts.
  - **Acceptance:** Mathematical identity verified: $\sum \Delta \text{StockValue} == \sum \Delta \text{GL.WarehouseAccount}$.

- [ ] **Task 3.5: Negative Stock Guard**
  - **Action:** Enforce strict policy preventing physical quantity from dropping below zero.
  - **Acceptance:** Consuming more than available on-hand balance throws `InsufficientStockException`.

- [ ] **Task 3.6: React Kardex & Stock Balance Viewer**
  - **Action:** Build `StockOverview.tsx` in `erp-client` showing warehouse cards, item balances, and immutable Kardex movements.
  - **Acceptance:** Real-time stock valuation and quantities render accurately.
