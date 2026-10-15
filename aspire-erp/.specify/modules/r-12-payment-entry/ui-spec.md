# Spec — Payment Entry Completion & In-Depth Invoice Settlement UI

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, RFC 7807, Modern React + Tailwind UI**

---

## 0. Propósito

El backend de `PaymentEntry` (R-12) cuenta con soporte transaccional completo: numeración sin huecos (`UPDLOCK/HOLDLOCK`), `RowVersion` optimista, conservación de montos, e idempotencia en submit/cancel.

Esta especificación cubre el frontend para:
1. **Vista de Detalle de Pago (`PaymentEntryDetailModal` o Página dedicada)**: Inspección de comprobante emitido, estatus contable, desglose de facturas liquidadas y visualización del asiento contable resultante.
2. **Asignación inteligente y anticipos**: En el modal de creación, selector automático de facturas con botón "Liquidar pendientes automáticamente (FIFO)" y cálculo en tiempo real de `UnallocatedAmount` (Anticipo).
3. **Filtros por Estado y Tercero en Listado**: Poder filtrar por `Draft`, `Submitted`, `Cancelled`, por Cliente/Proveedor o por Rango de Fechas.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Detalle de Pago | Al hacer clic en un registro de la lista no abre detalle | Implementar vista/modal de detalle con tabs: General, Facturas Asignadas, Asiento Contable (GL) | Componente `PaymentEntryDetailDialog.tsx` invocable al hacer clic en fila |
| D2 | Asignación Rápida | Asignación manual fila por fila en el modal | Botón "Auto-asignar por antigüedad" (FIFO) distribuyendo el `paidAmount` | Lógica pura en frontend que itera facturas ordenadas por fecha de vencimiento |
| D3 | Visualización de Anticipo | Campo `Unallocated` informativo | Alerta visual cuando hay excedente indicando que se creará como Anticipo a Favor | Banner informativo y validación de dirección estricta |
| D4 | Impresión de Recibo | No existe formato imprimible | Generar vista imprimible o PDF del Comprobante de Pago | Botón "Imprimir Comprobante" con estilos `@media print` |

---

## 2. Escenarios Gherkin (PAY-UI-G1..PAY-UI-G3)

```gherkin
Feature: Payment Entry UI Completion
  Background:
    Given Customer "ACME Corp" with 2 unpaid invoices: INV-01 ($300) and INV-02 ($200)

  Scenario: PAY-UI-G1 Auto-asignación FIFO de pagos
    When the user creates a Receive payment for ACME Corp of amount $400
    And clicks "Auto-allocate"
    Then INV-01 receives $300 allocation
    And INV-02 receives $100 allocation
    And Unallocated Amount reflects $0

  Scenario: PAY-UI-G2 Registro con anticipo (Unallocated > 0)
    When the user creates a Receive payment for $600
    And total outstanding is $500
    Then allocations sum to $500
    And the UI displays a highlight badge "Anticipo: $100"
    And allows saving the Draft

  Scenario: PAY-UI-G3 Inspección de Detalle y Asiento Contable
    Given a Submitted payment entry
    When the user clicks on the voucher row
    Then a detail dialog shows the General Ledger lines generated (Dr Bank, Cr Customer)
```

---

## 3. Plan de Tareas Frontend

- **T1**: Crear `PaymentEntryDetailDialog.tsx` con tabs (Datos Generales, Facturas Liquidadas, Póliza Contable).
- **T2**: Extender `PaymentEntryModal.tsx` con botón "Auto-asignar (FIFO)" y badges de anticipo.
- **T3**: Conectar evento `onRowClick` en [PaymentEntryList.tsx](file:///c:/Workspace/Odoo/aspire-erp/src/Frontend/erp-client/src/features/banking/components/PaymentEntryList.tsx) para abrir el detalle.
- **T4**: Añadir filtros por estado (`DocumentStatus`), tipo (`Receive` / `Pay`) y búsqueda por texto en la cabecera del listado.
