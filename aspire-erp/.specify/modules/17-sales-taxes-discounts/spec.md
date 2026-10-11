> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Spec: Sales Taxes & Global Discounts

## 1. Domain Overview
Para garantizar cálculos financieros exactos y compatibilidad con el MCP de ERPNext, la factura de venta (`SalesInvoice`) debe soportar Descuentos Globales y un desglose tabular de Impuestos (Múltiples tasas impositivas).

## 2. DDL & Schema Changes
- **`SalesInvoice`:** 
  - Agregar `DiscountPercentage` (decimal) y `DiscountAmount` (decimal).
  - Agregar colección `Taxes` (`SalesInvoiceTax`).
- **Nueva Entidad `SalesInvoiceTax`:**
  - `SalesInvoiceId` (Guid)
  - `AccountId` (Guid) -> La cuenta contable de pasivo (IVA Débito).
  - `Rate` (decimal) -> Porcentaje (ej. 21.00).
  - `TaxAmount` (decimal).
  
## 3. Business Rules (ERPNext Compliance)
- El descuento global (`DiscountAmount`) se aplica sobre el `NetTotal` (suma de los ítems antes de impuestos).
- Los impuestos se calculan sobre el `NetTotal` modificado por el descuento.
- `GrandTotal` = (`NetTotal` - `DiscountAmount`) + `TaxTotal`.
- **GL Posting (Idempotencia):** Al someter la factura, el `SalesPostingService` debe acreditar (Credit) en la cuenta contable definida en cada `SalesInvoiceTax` por su respectivo `TaxAmount`.
