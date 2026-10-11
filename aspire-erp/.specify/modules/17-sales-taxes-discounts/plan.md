> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Plan: Sales Taxes & Global Discounts

## Phase 1: Entity & Database (EF Core)
1. Crear entidad `SalesInvoiceTax.cs`.
2. Actualizar `SalesInvoice.cs` agregando `DiscountAmount`, `DiscountPercentage` y la colección `Taxes`.
3. Agregar las configuraciones (`EntityTypeConfiguration`) y generar la migración.

## Phase 2: CQRS & Posting Service
1. Actualizar `CreateSalesInvoiceCommand` y `UpdateSalesInvoiceCommand` para recibir listas de impuestos y descuentos.
2. Actualizar el motor de cálculo en el manejador para respetar la ecuación: `GrandTotal` = `NetTotal` - `Discount` + `Taxes`.
3. Modificar `SalesPostingService.cs` para iterar sobre los `Taxes` y generar entradas en el Ledger (Crédito a pasivos de impuestos).

## Phase 3: Frontend
1. En `SalesInvoiceForm.tsx`, agregar un bloque debajo de los ítems para ingresar `DiscountPercentage` / `Amount`.
2. Agregar una grilla "Taxes and Charges" para seleccionar la cuenta de impuestos y el porcentaje.
3. Actualizar la tabla de Totales visual (Neto, Descuento, Impuestos, Grand Total).
