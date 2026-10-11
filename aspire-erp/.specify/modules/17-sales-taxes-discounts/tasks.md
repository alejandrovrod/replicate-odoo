> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Tasks: Sales Taxes & Global Discounts

- [x] `DB`: Crear `SalesInvoiceTax.cs` y agregarlo a `SalesInvoice.cs` junto a `DiscountAmount`. Crear migración EF Core.
- [x] `Backend`: Actualizar DTOs y Comandos (`CreateSalesInvoiceCommand`, etc.) para incluir `Taxes` y `DiscountAmount`.
- [x] `Backend`: Actualizar `CreateSalesInvoiceCommandHandler` para recalcular los totales matemáticos exactos.
- [x] `Backend`: Refactorizar `SalesPostingService` para insertar asientos (GL Entries) por cada línea de impuesto.
- [x] `Frontend`: Agregar componente `DiscountSection` en la UI de creación de factura.
- [x] `Frontend`: Agregar tabla dinámica `TaxesGrid` en la UI para seleccionar cuentas contables e ingresar el porcentaje.
- [x] `Frontend`: Actualizar validaciones de formulario para evitar impuestos con cuentas de ingresos (debe ser pasivo/liability).
