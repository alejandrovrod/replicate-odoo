# Tasks — CRM Parity vs ERPNext

## Fase 1: Dominio & Persistencia Backend (.NET 10)
- [x] **T1.1 (DDL & Migración):** `AddCrmParityOpportunityItemsQuotation` — tablas `OpportunityItem`, `OpportunityLossReason`, `Territory`, `Industry`; columnas en `Opportunity` (`ExchangeRate`, `BaseOpportunityAmount`, `LossReasonId`, `TerritoryId`, `IndustryId`, `MarketSegment`, `Competitors` JSON) y `OpportunityId` en `Quotations`. Índices por `TenantId`/`CompanyId`/`Stage` (existente `IX_Opportunity_Tenant_Stage` intacto).
- [x] **T1.2 (Entidades):** `OpportunityItem`, catálogos; `Opportunity.RecalculateTotals()` + `MoveToStage()` + `MarkAsClosedLost(id, notes)`; `Quotation.OpportunityId`.
- [x] **T1.3 (Unit Tests Dominio):** `OpportunityTests` — totales/base, transiciones, motivo normalizado.

## Fase 2: Aplicación CQRS
- [x] **T2.1:** `GetOpportunityDetailQuery`, `UpdateOpportunityCommand` (transaccional), `CreateQuotationFromOpportunityCommand`, `AdvanceOpportunityStageCommand` extendido con `LossReasonId`.
- [x] **T2.2:** Resolución de cliente (precedente sales-order), moneda viva con fallback, catálogos con auto-seed por tenant.
- [x] **T2.3:** `IQuotationRepository` + `ICrmCatalogRepository` (implementados por `CrmRepository`, mismo DbContext).

## Fase 3: Controladores & API Contracts
- [x] **T3.1:** `GET {id}`, `PUT {id}`, `POST {id}/create-quotation` en `OpportunitiesController`; `QuotationsController` (GET); `CrmCatalogsController` (3 GETs). Mapeos 404/409/400 en `OpportunityProblem`.
- [x] **T3.2 (Integration Tests):** `CrmParityApiTests` (8) + actualización de `CrmLifecycleApiTests` al contrato normalizado.

## Fase 4: Frontend (React 19 / TypeScript)
- [x] **T4.1:** tipos estrictos (`OpportunityItemDto`, `OpportunityDetailDto`, `CreateQuotationPayload`, catálogos) + `crmApi` extendida con `AbortSignal`.
- [x] **T4.2:** Kanban con `useOptimistic`, diálogo accesible de motivo (adiós `window.prompt`), "Crear Cotización" primaria + orden directa secundaria, `OpportunityDetailModal` con pestañas (líneas editables con totales reactivos, segmentación, historial).
- [x] **T4.3:** i18n completo `crm.json` en+es + tipos regenerados + Vitest (8 tests).
