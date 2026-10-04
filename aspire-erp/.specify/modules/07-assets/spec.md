# Functional Specification: Asset Management & Depreciation (ERPNext Parity)

**Module:** `07-assets`  
**Status:** IN PROGRESS — Block A implemented (tasks 10.1, 10.2, 10.3); Block B/C pending  
**Version:** 2.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext Assets Documentation](https://docs.frappe.io/erpnext/assets)  

---

## 1. Executive Summary & Ubiquitous Language

The **Asset Management Module** tracks the complete financial and physical lifecycle of capital goods (machinery, vehicles, computers, buildings). It manages capital procurement, Capital Work In Progress (CWIP) capitalization, automated straight-line and declining depreciation schedules, physical location transfers, and end-of-life disposal (sale or scrapping) with automated gain/loss ledger recognition.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Asset** | `Asset` | Capital property record holding identification, serial numbers, gross purchase value, custodian, location, and depreciation schedule. |
| **Asset Category** | `Asset Category` | Configuration template linking a class of assets to GL accounts (Fixed Asset, Accumulated Depreciation, Depreciation Expense, CWIP). |
| **Capitalization** | Asset Capitalization | Recognition event transferring costs from CWIP or vendor purchase into the active Fixed Asset ledger account. |
| **Depreciation Schedule** | `Depreciation Schedule` | Time-series schedule of planned depreciation postings dividing depreciable value across the useful life. |
| **Accumulated Depreciation** | Contra Asset Account | Balance sheet negative asset account accumulating historical depreciation charges. |
| **Net Book Value (NBV)** | Net Book Value | The remaining unamortized carrying value ($\text{GrossPurchaseAmount} - \text{AccumulatedDepreciation}$). |
| **Asset Disposal** | Asset Sale / Scrap | Termination transaction removing gross asset value and accumulated depreciation from the books and recognizing gain or loss. |

---

## 2. Core Business Invariants & Accounting Rules

### Invariant AS-01: Straight-Line Periodic Depreciation Allocation
For an asset with straight-line depreciation:
$$\text{PeriodicDepreciation} = \frac{\text{GrossPurchaseAmount} - \text{SalvageValue}}{\text{TotalNumberOfDepreciations}}$$
- The Net Book Value can never depreciate below the `SalvageValue`:
  $$\text{NetBookValue} \ge \text{SalvageValue}$$

### Invariant AS-02: Double-Entry Periodic Depreciation Posting
For each scheduled depreciation date:
- **Debit:** `AssetCategory.DepreciationExpenseAccountId` = $\text{DepreciationAmount}$
- **Credit:** `AssetCategory.AccumulatedDepreciationAccountId` = $\text{DepreciationAmount}$

### Invariant AS-03: Double-Entry Asset Disposal Balancing
Upon disposal (Sale or Scrap):
- **Debit:** `AccumulatedDepreciationAccountId` (total accrued depreciation to date)
- **Debit:** `Bank / Accounts Receivable` (proceeds if sold, $0 if scrapped)
- **Debit / Credit:** `Loss / Gain on Asset Disposal Account` (balancing variance)
- **Credit:** `FixedAssetAccountId` (original gross purchase cost)
$$\sum \text{Debit} - \sum \text{Credit} == 0.0000$$

---

## 3. Gherkin Functional Scenarios

### Scenario AS-01: Asset Capitalization and Schedule Generation
- **Given** item `Dell Precision Laptop` purchased for $2,400.00 with $0.00 salvage value
- **And** useful life configured as 24 months (straight line) under category `IT Hardware`
- **When** the asset is capitalized on `2026-10-01`
- **Then** a schedule with 24 monthly lines of $100.00/month is created
- **And** `Asset.Status` becomes `Capitalized`.

### Scenario AS-02: Automated Monthly Depreciation Booking
- **Given** capitalized asset `AST-2026-0001` with monthly schedule line due on `2026-10-31` for $100.00
- **When** the scheduled depreciation processor executes
- **Then** `GLEntry` records:
  - Debit: `5310 - Depreciation Expense` ($100.00)
  - Credit: `1520 - Accumulated Depreciation (IT Hardware)` ($100.00)
- **And** `Asset.NetBookValue` reduces from $2,400.00 to $2,300.00.

### Scenario AS-03: Asset Sale with Gain on Disposal
- **Given** an asset with Gross Value $10,000.00 and Accumulated Depreciation $6,000.00 (NBV = $4,000.00)
- **When** the company sells the asset for $4,500.00 cash
- **Then** `GLEntry` records:
  - Debit: `1110 - Bank Account` ($4,500.00 proceeds)
  - Debit: `1520 - Accumulated Depreciation` ($6,000.00)
  - Credit: `1510 - Fixed Asset Equipment` ($10,000.00 original cost)
  - Credit: `4220 - Gain on Asset Disposal` ($500.00)
- **And** `Asset.Status` becomes `Sold`.

### Scenario AS-04: Idempotent Scheduled Depreciation Booking
- **Given** monthly scheduled depreciation job triggering on `2026-10-31` with `Idempotency-Key: idemp-dep-2026-10`
- **When** the scheduler runs twice due to a container restart
- **Then** already booked schedule lines (`IsBooked = 1`) are detected and skipped
- **And** duplicate `GLEntry` depreciation expenses are strictly prevented.

### Scenario AS-05: Asset Scrapping with Full Loss on Disposal
- **Given** an unrepairable broken machine with Gross Value $5,000.00 and Accumulated Depreciation $3,000.00 (NBV = $2,000.00)
- **When** the asset is scrapped with $0.00 salvage proceeds
- **Then** `GLEntry` records:
  - Debit: `1520 - Accumulated Depreciation` ($3,000.00)
  - Debit: `5320 - Loss on Asset Disposal` ($2,000.00)
  - Credit: `1510 - Fixed Asset Equipment` ($5,000.00)
- **And** remaining future scheduled depreciation lines are cancelled (`Status = Scrapped`).

### Scenario AS-06: Concurrency Guard on Simultaneous Asset Disposal & Depreciation
- **Given** asset `AST-2026-004` being disposed of by an accountant
- **When** an automated background depreciation job attempts to book monthly depreciation simultaneously
- **Then** row locking on `Asset` detects the state transition
- **And** exactly one operation commits; the conflicting depreciation job aborts cleanly.

