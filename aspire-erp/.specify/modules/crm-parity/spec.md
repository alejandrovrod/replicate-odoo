# Spec — CRM Parity vs ERPNext (Opportunity Items, Segmentation, Quotation Flow)

> **STATUS: IMPLEMENTED**
> **Standards: Clean Architecture, ERPNext Parity, RFC 7807, snake_case codes (`crm_*`)**

## 0. Propósito

Evolucionar el pipeline CRM (`#/crm`) desde el Kanban de monto plano hacia la paridad
funcional con ERPNext: líneas valorizadas por oportunidad, ficha comercial (segmentación,
competidores, multi-moneda con base convertida), motivos de pérdida normalizados y el
flujo cotización-antes-que-orden (`Opportunity → Quotation → Sales Order`).

## 1. Decisiones arquitectónicas (verificadas contra código)

| # | Decisión | Motivo |
|---|----------|--------|
| D1 | Sin MediatR ni Minimal APIs: `ISender`/`ICommandHandler` + Controllers con DI explícita | Convención vigente del codebase (sin assembly scanning) |
| D2 | Sin `POST /{id}/items` granular: el PUT de ficha lleva la lista completa | Una sola ruta de escritura, sin carreras de updates parciales |
| D3 | Reutilización de `PaymentAllocation`-style: `OpportunityItem` nuevo, pero `QuotationItem` y `PaymentEntry` existentes intactos | Sin tablas duplicadas |
| D4 | Catálogos (motivos/territorios/industrias) de solo lectura con auto-seed por tenant en primera lectura + `seed-dev-crm.sql` | Un tenant fresco debe cerrar tratos el día uno sin paso de admin |
| D5 | Sin asiento compensatorio en el cruce: N/A (las cotizaciones no postean GL) | — |
| D6 | `UpdateOpportunityAsync`: marcado explícito solo-raíz (`Entry().State = Modified`), prohibido `Update()` blanket | `Update()` degrada líneas nuevas con clave a UPDATEs de 0 filas (409 fantasma); sin marcado, el perdedor de un race devuelve 200 fantasma (regresión CRM-06 probada) |
| D7 | Líneas por `ReplaceOpportunityItemsAsync` explícito (RemoveRange/AddRange) dentro de la transacción del update | Estados determinísticos bajo cualquier modo de tracking |

## 2. Escenarios

```gherkin
Scenario: CRM-P1 Ficha con líneas recalcula totales y base
  Given a deal of $50,000 without lines
  When updating with lines [2 × $100] and EUR rate 1.10 (company USD)
  Then OpportunityAmount = $200, BaseOpportunityAmount = $220

Scenario: CRM-P2 Cotización desde ClosedWon arrastra líneas
  Given a Won deal with priced lines
  When POST /opportunities/{id}/create-quotation
  Then 201 QTN-YYYY-NNNNN Draft with the deal lines, note activity attached

Scenario: CRM-P3 Cierre con motivo normalizado
  When advancing to ClosedLost with lossReasonId + notes
  Then LossReasonId FK set, LossReason keeps the notes; unknown id → 400
```

## 3. API

- `GET /api/v1/opportunities/{id}` — dossier (líneas, actividades, cotizaciones)
- `PUT /api/v1/opportunities/{id}` — edición de ficha (RowVersion mandatorio, 409 en race)
- `POST /api/v1/opportunities/{id}/create-quotation` — 201 + Location
- `GET /api/v1/quotations/{id}` — lectura de cotización
- `GET /api/v1/loss-reasons|territories|industries` — catálogos de solo lectura
- `POST /{id}/advance` acepta `lossReasonId` (el texto libre auto-matchea catálogo por compatibilidad)

## 4. Errores (RFC 7807)

- `crm_opportunity_not_open` (409) — edición en trato cerrado
- `crm_invalid_opportunity_item` (400) — línea inválida
- `crm_quotation_requires_items` (400) — cotización sin líneas
- `crm_quotation_not_found` (404) — lectura de cotización
- `crm_loss_reason_not_found` / `crm_territory_not_found` / `crm_industry_not_found` (400)
- `crm_invalid_exchange_rate` (400)
