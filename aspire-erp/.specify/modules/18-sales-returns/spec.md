> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Spec: Sales Returns & Credit Notes

## 1. Domain Overview
El sistema debe soportar el registro de Devoluciones / Notas de Crédito basándose en la especificación de ERPNext. Una factura marcada como `IsReturn = true` representa una reversión parcial o total de la deuda de un cliente y revierte ingresos e impuestos.

## 2. DDL & Schema Changes
- **`SalesInvoice`:**
  - Agregar `IsReturn` (booleano, por defecto falso).
  - Agregar `ReturnAgainstId` (Guid, Nullable), que hace FK hacia la misma tabla `SalesInvoice` (Id de la factura original).

## 3. Business Rules (ERPNext Compliance)
- **Validación:** Si `IsReturn == true`, entonces `ReturnAgainstId` es obligatorio.
- **Totales Negativos:** Todas las cantidades en los ítems, y los importes (`NetTotal`, `TaxTotal`, `GrandTotal`) deben ser negativos.
- **Validación de Límite:** El valor absoluto del `GrandTotal` de la nota de crédito no puede exceder el `GrandTotal` de la factura original (`ReturnAgainstId`).
- **GL Posting:** El `SalesPostingService` debe invertir las partidas. En lugar de Debitar Clientes (A/R) y Acreditar Ingresos, debe Acreditar Clientes (A/R) y Debitar Ingresos/Impuestos.
- **Stock:** Si `UpdateStock == true`, una Nota de Crédito re-ingresa inventario al almacén origen (Crédito a COGS, Débito a Inventario).
