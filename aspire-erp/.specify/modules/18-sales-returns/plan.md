> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Plan: Sales Returns & Credit Notes

## Phase 1: Entity & Database
1. Agregar `IsReturn` y `ReturnAgainstId` a `SalesInvoice.cs`.
2. Actualizar configuración EF Core (Self-referencing FK).
3. Generar migración.

## Phase 2: Backend CQRS
1. Validaciones: En `CreateSalesInvoiceCommandHandler`, asegurar que si es nota de crédito, los valores sean negativos y referencien a una factura enviada.
2. Contabilidad: Ajustar `SalesPostingService` para detectar `IsReturn`. Multiplicar por -1 los importes si se aprovecha el mismo motor, o generar ramas if/else claras para revertir el balance contable.
3. Actualizar endpoints de la API (opcional: agregar Query para buscar facturas elegibles para devolución).

## Phase 3: Frontend
1. En `SalesInvoiceForm.tsx`, agregar checkbox "Is Return (Credit Note)".
2. Si se tilda el checkbox, mostrar un Dropdown/Selector para elegir la "Return Against Invoice".
3. Al seleccionar la factura, autocompletar las líneas con cantidades negativas.
