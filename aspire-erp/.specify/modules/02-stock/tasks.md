# Implementation Tasks: Stock & Perpetual Inventory (ERPNext Parity)

**Module:** `02-stock`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** IN PROGRESS  

---

## Phase 3: Multi-Warehouse Inventory & Kardex FIFO

- [x] **Task 3.1: Domain Entities `Item`, `Warehouse`, and `UOM`**
  - **Action:** Implement `Item` (with valuation method FIFO), `Warehouse` (with account mapping), and `UOM` in `Erp.Domain.Stock`.
  - **Acceptance:** Validates that warehouse accounts resolve through hierarchy to company defaults.

- [x] **Task 3.2: Stock Movements & `StockEntry` Aggregate**
  - **Action:** Create `StockEntry` supporting MaterialReceipt, MaterialIssue, and MaterialTransfer with item validation.
  - **Acceptance:** Cannot transfer materials to the identical warehouse; validates that item exists and is active.

- [x] **Task 3.3: FIFO Costing Engine & `StockLedgerEntry` (SLE)**
  - **Action:** Implement `FifoCostEngine` and `StockLedgerEntry` recording running physical quantity and stock value.
  - **Acceptance:** Inbound receipts create new cost batches; outbound issues consume oldest available layers.

- [x] **Task 3.4: Perpetual Inventory Accounting Integration**
  - **Action:** Submitting a `StockEntry` automatically books balanced `GLEntry` records linking warehouse accounts with COGS or Stock Adjustment accounts.
  - **Acceptance:** Mathematical identity verified: $\sum \Delta \text{StockValue} == \sum \Delta \text{GL.WarehouseAccount}$.

- [x] **Task 3.5: Negative Stock Guard**
  - **Action:** Enforce strict policy preventing physical quantity from dropping below zero.
  - **Acceptance:** Consuming more than available on-hand balance throws `InsufficientStockException`.

- [x] **Task 3.6: React Kardex & Stock Balance Viewer**
  - **Action:** Build `StockOverview.tsx` in `erp-client` showing warehouse cards, item balances, and immutable Kardex movements.
  - **Acceptance:** Real-time stock valuation and quantities render accurately.

- [x] **Task 3.7: Stock Movement Cancellation & Compensating SLEs**
  - **Action:** Implement `CancelStockEntryCommandHandler` flagging `IsCancelled = 1` and appending negative quantity SLEs + reversing GLEntry records.
  - **Acceptance:** Stock balance and financial accounts are restored to pre-transaction states without deleting database history.

- [x] **Task 3.8: Idempotency & Duplicate Submission Pipeline**
  - **Action:** Implement ASP.NET Core idempotency middleware / behavior checking `Idempotency-Key` header with Redis / SQL cache.
  - **Acceptance:** Replaying an identical submission returns HTTP 200 with the cached response and creates zero duplicate SLEs or GLEntries.

- [ ] **Task 3.9: Concurrency & Stress Integration Testing**
  - **Action:** Write adversarial multi-threaded integration test issuing concurrent stock requests against low inventory.
  - **Acceptance:** Database optimistic concurrency / row locks prevent overselling; exactly available units are issued, excess requests throw `InsufficientStockException`.

