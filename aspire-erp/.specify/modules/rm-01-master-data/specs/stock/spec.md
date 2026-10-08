# RM-01: Master Data UI (Warehouses) Spec

## Feature Description
This feature provides the first iteration of the generic Master Data UI pattern by implementing the Warehouses catalog in the frontend, along with missing PUT/DELETE backend endpoints. 

## Requirements
1. **List View:**
   - A data table displaying all Warehouses.
   - Must support pagination.
   - Must support filtering/searching.
   - Must display columns: Name, Type (e.g. Group vs Ledger), IsActive.
2. **Form View:**
   - A modal or dedicated page to create/edit a Warehouse.
   - Fields: Name (string, required), WarehouseType (enum, required), IsActive (boolean), IsGroup (boolean), ParentWarehouse (optional).
   - Form validation displaying errors to the user.
3. **Backend API:**
   - Expose `PUT /api/stock/warehouses/{id}` with `RowVersion` concurrency check.
   - Expose `DELETE /api/stock/warehouses/{id}` or a disable endpoint.
4. **Shared Components:**
   - The UI components for Table, Modal, and Forms must be built using accessible components (e.g., shadcn/ui or equivalent raw Tailwind+Radix components).
   - They must be generic enough to reuse for Items, Customers, etc.

## Scenarios
- **Scenario 1: View Warehouses**
  - Given the user navigates to `/stock/warehouses`,
  - Then they see a paginated list of existing warehouses from the backend.
- **Scenario 2: Create Warehouse**
  - Given the user clicks "New",
  - When they fill the form and submit,
  - Then the warehouse is created in the backend and the list updates.
- **Scenario 3: Edit Warehouse**
  - Given the user clicks on a warehouse in the list,
  - When they modify the details and submit,
  - Then the backend updates the warehouse via `PUT` with concurrency checks.
- **Scenario 4: Concurrent Edit Conflict**
  - Given two users open the same warehouse,
  - When User A saves successfully,
  - And User B attempts to save,
  - Then User B receives a concurrency error and must reload.
