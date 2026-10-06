# RM-01: Master Data UI Pattern & Warehouses Design

## Architecture & Tech Stack
**Frontend:** React 19, TypeScript, Vite, React Router, React Query.
**UI Components:** Custom components structured via Tailwind CSS (and Radix primitives if needed). We will create `DataTable`, `Modal`, `Button`, and `Input` inside `src/components/ui`.
**Backend:** C# ASP.NET Core Minimal APIs for HTTP endpoints, CQRS via MediatR, Entity Framework Core for DB.

## Component Design
### `MasterDataList<T>`
A generic table component that takes `columns` and `data` definitions, integrating pagination controls.
### `MasterDataForm<T>`
A wrapper component that handles form state and validation display.
### `WarehouseView` (in `src/features/stock/pages/WarehouseView.tsx`)
Connects React Query `useWarehouses()` hook to the `MasterDataList` and renders the "New" button.
### `WarehouseFormModal` (in `src/features/stock/components/WarehouseFormModal.tsx`)
A modal that contains the form for a Warehouse, submitting to `useCreateWarehouse()` or `useUpdateWarehouse()` mutations.

## Backend Changes
- Modify `src/Backend/Erp.Api/Endpoints/StockEndpoints.cs` to add `MapPut` and `MapDelete` for Warehouses.
- Create `UpdateWarehouseCommand` and `UpdateWarehouseCommandHandler`.
- Include concurrency handling: The Command must accept a `RowVersion` (byte array) and validate it against the DB using EF Core's optimistic concurrency token mechanism.

## API Contracts
**PUT /api/stock/warehouses/{id}**
Request body:
```json
{
  "name": "Updated Name",
  "warehouseType": "Ledger",
  "isActive": true,
  "isGroup": false,
  "parentWarehouseId": null,
  "rowVersion": "base64-encoded-string"
}
```
Response: 200 OK with the updated DTO, or 409 Conflict if `RowVersion` mismatched.
