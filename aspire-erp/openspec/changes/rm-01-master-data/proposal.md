# Proposal: RM-01 Master Data UI Pattern & Warehouses

## Problem
Currently, Aspire ERP lacks a user interface for creating, editing, and disabling master data (catalogs). The API has `POST` endpoints for these entities, but the frontend (`erp-client`) only reads data populated via SQL seed scripts. Without a UI, users cannot configure their ERP instance, and domain rules are bypassed if data is modified directly in the database.

## Proposed Solution
We will implement a shared **Master Data UI pattern** in the frontend, composed of:
1. **List View:** A generic data table with search, pagination, and status filtering.
2. **Form View:** A generic modal or page to create and edit catalog items, including validation.
3. **API Integration:** Shared React Query hooks for fetching, creating, updating, and disabling records.

As the first implementation of this pattern, we will build the **Warehouses** catalog (Task RM-04). This will establish the pattern and validate the UI components. We will use a standard UI library (like `shadcn/ui` components built on Tailwind and Radix) to populate the currently empty `src/components/ui` directory.

## Scope
- Implement generic `MasterDataList` and `MasterDataForm` components.
- Setup `shadcn/ui` (or equivalent accessible UI components) for tables, inputs, buttons, and modals.
- Create the `/stock/warehouses` route.
- Wire the UI to the existing `POST /api/stock/warehouses` and `GET` endpoints.
- Add `PUT` (edit) and `DELETE` (disable) endpoints for Warehouses in the backend API, since they are missing (Task RM-08).

## Non-Goals
- We will not build UIs for *all* catalogs in this change. We will only build Warehouses. Other catalogs (Items, Customers, Chart of Accounts) will follow in subsequent changes using this pattern.

## Risks & Trade-offs
- The frontend currently has no UI components library (`ui` folder is empty). We need to introduce one, which might require a slight learning curve, but guarantees long-term consistency.
- Optimistic concurrency control (RowVersion) must be respected in the `PUT` endpoints to prevent lost updates.
