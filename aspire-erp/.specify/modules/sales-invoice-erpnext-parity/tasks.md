# Tasks — Sales Invoice ERPNext Parity (Direct Stock & Advances)

## Fase 1: Dominio & Persistencia Backend (.NET 10)
- [x] **T1.1 (DDL & Migración):**
  - Agregar columnas `UpdateStock`, `SourceWarehouseId`, `TotalAdvance` a `SalesInvoices`.
  - Crear tabla `SalesInvoiceAdvances` con foreign key a `PaymentEntries` y `SalesInvoices`.
  - *Nota de implementación: `UpdateStock`/`SourceWarehouseId` ya existían en el esquema; la migración `AddSalesInvoiceAdvanceParity` agrega solo `TotalAdvance`. La tabla relacional se reutilizó como `PaymentAllocation` (ya tenía FK a `SalesInvoice` + constraint de factura única) en lugar de crear `SalesInvoiceAdvances` duplicada.*
- [x] **T1.2 (Entidades de Dominio):**
  - Extender `SalesInvoice` con el método `AllocateAdvance` y reglas de guardia para validación de almacén y stock.
- [x] **T1.3 (Unit Tests):** Pruebas unitarias en `Erp.Domain.UnitTests` validando cruce de anticipos y límites de saldo.

## Fase 2: Aplicación CQRS & Servicios de Asentamiento
- [x] **T2.1 (Commands & DTOs):**
  - Modificar `CreateSalesInvoiceCommand` para soportar `updateStock`, `sourceWarehouseId` y `advances`.
- [x] **T2.2 (Posting Service - Kardex & DoubleEntry):**
  - Si `updateStock = true`, invocar el servicio de valuación FIFO para generar `StockLedgerEntry` de salida y líneas de General Ledger (`Dr 5101 COGS / Cr 1104 Inventario`).
  - Si hay `advances`, generar asiento compensatorio (`Dr Anticipos / Cr Cuentas por Cobrar`) y marcar los `PaymentAllocation` correspondientes.
  - *Nota de implementación: el cruce NO genera asiento compensatorio (ver decisión en Fase 2 del resumen): el cobro ya acreditó Cuentas por Cobrar en su totalidad, por lo que el cruce netea dentro de Cuentas por Cobrar por construcción. Se crean los `PaymentAllocation`, se consume `UnallocatedAmount` y se actualiza `OutstandingAmount`/`PaidAmount`/estado.*
- [x] **T2.3 (Query de Anticipos Elegibles):**
  - Implementar `GetCustomerEligibleAdvancesQuery` para buscar pagos con saldo a favor.

## Fase 3: Controladores & API Contracts
- [x] **T3.1 (Endpoints):**
  - Exponer `GET /api/v1/customers/{customerId}/eligible-advances`.
  - Actualizar `POST /api/v1/sales-invoices` y `POST /api/v1/sales-invoices/{id}/submit`.
- [x] **T3.2 (Integration Tests):**
  - Pruebas de integración comprobando el impacto simultáneo en GL y Kardex al enviar la factura.

## Fase 4: Frontend (React 19 / TypeScript)
- [x] **T4.1 (API Client & Hooks):**
  - Crear hook `useEligibleAdvances(customerId)` y actualizar tipos en `useSalesInvoices.ts`.
- [x] **T4.2 (Modal de Factura):**
  - En `SalesInvoiceFormModal.tsx`:
    - Agregar switch `Actualizar inventario (Entrega inmediata)` y selector de almacén.
    - Agregar sección o tabla de `Anticipos a cruzar` con selector de pagos disponibles.
- [x] **T4.3 (Grilla y Badges):**
  - Agregar badges en el listado para distinguir facturas con entrega de stock directa y facturas pagadas con anticipos.
