# Functional Specification: Manufacturing & Production (ERPNext Parity)

**Module:** `06-manufacturing`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext Manufacturing Documentation](https://docs.frappe.io/erpnext/manufacturing)  

---

## 1. Executive Summary & Ubiquitous Language

The **Manufacturing Module** bridges engineering specifications with shop floor execution and cost accounting. It translates product designs into hierarchical **Bills of Materials (BOM)**, schedules production runs through **Work Orders**, tracks labor and machine costs across **Workstations**, and manages inventory transitions from raw materials to Work In Progress (WIP) and finished goods.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Bill of Materials (BOM)** | `BOM` | Master recipe detailing raw material quantities, scrap percentages, and workstation operations required to produce a finished item. |
| **Work Order (WO)** | `Work Order` | Production authorization scheduling production quantity, planned start/end dates, and associated warehouses. |
| **Workstation** | `Workstation` | Equipment, machine, or bench where manufacturing operations occur, with configured hourly operating and labor rates. |
| **Job Card** | `Job Card` | Shop floor task card tracking operator time logs, actual production duration, and workstation usage. |
| **Work In Progress (WIP)** | `WIP Warehouse` | Inventory transit warehouse and balance sheet asset holding materials actively undergoing transformation. |
| **Cost Capitalization** | Valuation Capitalization | Method absorbing raw materials, direct machine hours, and labor into the unit valuation rate of the completed finished item. |
| **Scrap Item** | `BOM Scrap Item` | Usable byproduct or waste produced during manufacture with defined salvage valuation or write-off. |

---

## 2. Core Business Invariants & Accounting Rules

### Invariant MF-01: Cost Capitalization Invariant
The unit valuation rate of a finished good produced via a `WorkOrder` is:
$$\text{UnitValuationRate} = \frac{\sum \text{RawMaterialCost} + \sum \text{OperatingCost} - \text{ScrapValue}}{\text{ProducedQuantity}}$$

### Invariant MF-02: Double-Entry Manufacturing Ledger Mechanics
1. **Material Transfer to WIP (`StockEntry: MaterialTransfer`):**
   - **Debit:** `1320 - Work In Progress Stock (WIP Warehouse)`
   - **Credit:** `1310 - Raw Material Inventory (Stores)`
2. **Manufacture Completion (`StockEntry: Manufacture`):**
   - **Debit:** `1330 - Finished Goods Inventory` = $\text{TotalCost}$
   - **Credit:** `1320 - Work In Progress Stock` = $\text{RawMaterialCost}$
   - **Credit:** `5210 - Expenses Included in Valuation (Labor/Overhead Absorption)` = $\text{OperatingCost}$
   $$\sum \text{Debit} - \sum \text{Credit} == 0.0000$$

### Invariant MF-03: Active BOM Guard
A `WorkOrder` cannot be created or submitted without referencing a valid, active (`IsActive == true`) BOM.

---

## 3. Gherkin Functional Scenarios

### Scenario MF-01: Active BOM Cost Calculation
- **Given** item `Finished Assembly` with components:
  - 2 units of `Component-A` @ $15.00 ($30.00)
  - 1 unit of `Component-B` @ $20.00 ($20.00)
  - 30 minutes assembly at Workstation `WS-01` ($40.00/hour = $20.00)
- **When** the engineer submits the `BOM`
- **Then** `TotalCost` is calculated as $70.00 ($50.00 materials + $20.00 operations)
- **And** the BOM is marked `IsActive = true` and `IsDefault = true`.

### Scenario MF-02: Issue Materials to Work In Progress (WIP)
- **Given** a submitted `WorkOrder` for 10 units of `Finished Assembly`
- **When** the shop floor transfers required components (20 units of `A`, 10 units of `B`) to `WIP Warehouse`
- **Then** `StockLedgerEntry` relieves components from `Stores` and adds them to `WIP Warehouse`
- **And** `GLEntry` debits `1320 - WIP Stock` ($500.00) and credits `1310 - Stores Stock` ($500.00).

### Scenario MF-03: Production Completion & Valuation Capitalization
- **Given** the 10 units of `Finished Assembly` have completed assembly
- **When** the `Manufacture` stock entry is posted
- **Then** 10 units of `Finished Assembly` are added to `Finished Goods Warehouse` @ $70.00/unit ($700.00)
- **And** `GLEntry` records:
  - Debit: `1330 - Finished Goods Stock` ($700.00)
  - Credit: `1320 - WIP Stock` ($500.00)
  - Credit: `5210 - Expenses Included in Valuation` ($200.00 operations absorption)
- **And** `WorkOrder.Status` becomes `Completed`.
