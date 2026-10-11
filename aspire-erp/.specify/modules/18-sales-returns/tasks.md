> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Tasks: Sales Returns & Credit Notes

- [x] `DB`: Agregar `IsReturn` y `ReturnAgainstId` en `SalesInvoice.cs` y correr migración EF.
- [x] `Backend`: Actualizar `CreateSalesInvoiceCommand` y `SubmitSalesInvoiceCommand` con lógica de cantidades negativas.
- [x] `Backend`: Actualizar `SalesPostingService.cs` para emitir GL Entries inversos (A/R a la baja, Ingresos a la baja, Inventario a la suba).
- [x] `Frontend`: Agregar checkbox "Is Return" en la vista de cabecera de la factura.
- [x] `Frontend`: Integrar selector para buscar facturas emitidas del mismo cliente en `ReturnAgainstId`.
- [x] `Frontend`: Implementar función para poblar y negativizar automáticamente los ítems de la factura original al seleccionarla.
