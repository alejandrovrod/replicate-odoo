# Tasks — Supplier Master Data & Governance (ERPNext Parity)

## Fase 1: Base de Datos & Dominio Backend (.NET 10)
- [ ] **T1.1 (DDL & Migración):** Crear migración EF Core para agregar `SupplierGroups`, tabla intermedia `SupplierCompanyAccounts` y nuevos campos de gobierno (`IsFrozen`, `OnHold`, `HoldType`, `ReleaseDate`, `SupplierGroupId`, `PaymentTermsTemplateId`, `DefaultPriceListId`).
- [ ] **T1.2 (Entidades & Agregado):** Extender entidad `Supplier` en `Erp.Domain` con métodos de gobierno (`SetHoldStatus`, `IsBlockedFor`, `SetCompanyAccount`).
- [ ] **T1.3 (Unit Tests Dominio):** Implementar pruebas unitarias validando bloqueo según fecha de vencimiento (`ReleaseDate`) y concurrencia optimista.

## Fase 2: Aplicación CQRS & Validadores (MediatR)
- [ ] **T2.1 (Commands & DTOs):**
  - Modificar `CreateSupplierCommand` y `UpdateSupplierCommand` para recibir `SupplierGroupId`, cuentas multi-empresa y configuración de Hold.
  - Implementar comando `SetSupplierHoldStatusCommand`.
- [ ] **T2.2 (Queries):**
  - Actualizar `GetSupplierByIdQuery` y `GetSuppliersQuery` para incluir cuentas por compañía y grupo.
- [ ] **T2.3 (Guards en Compras):**
  - Inyectar validación en `CreatePurchaseInvoiceCommand` y `CreatePurchaseOrderCommand` verificando `supplier.IsBlockedFor(HoldType, postingDate)`.

## Fase 3: Controladores & API Contracts
- [ ] **T3.1 (Endpoints):**
  - Exponer `GET /api/v1/supplier-groups`.
  - Exponer `POST /api/v1/suppliers/{id}/hold`.
  - Actualizar endpoints existentes de `Suppliers` para responder con el nuevo DTO estructurado.
- [ ] **T3.2 (Integration Tests):**
  - Pruebas de integración validando creación multi-empresa y rechazo 422 al facturar a un proveedor en retención.

## Fase 4: Frontend (React 19 / TypeScript)
- [ ] **T4.1 (API Client & Tipos):** Actualizar `Supplier` interface en `useSuppliers.ts` y crear hook `useSupplierGroups`.
- [ ] **T4.2 (UI - Tabs en Modal):**
  - Reestructurar `SupplierFormModal.tsx` con tabs:
    - **General:** Código, Nombre, CUIT/RUT, Grupo de Proveedor, Moneda.
    - **Contabilidad:** Selector de cuentas por pagar/anticipo por Empresa.
    - **Gobierno:** Switch `On Hold`, selector de `Hold Type` (Invoices/Payments/All) y selector de `Release Date`.
- [ ] **T4.3 (Listado & Filtros):** Agregar columna/badge visual de estado (Activo, Retenido/Bloqueado) y filtro por grupo en `SupplierView.tsx`.
