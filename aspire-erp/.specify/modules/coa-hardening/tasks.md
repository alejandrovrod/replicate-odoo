# Implementation Tasks: Plan de Cuentas (COA) Hardening

**Module:** `01-accounting / COA`
**Spec:** [spec.md](./spec.md) · **Plan:** [plan.md](./plan.md)
**Status:** CERTIFIED
**Version:** 1.0.0
**Date:** 2026-10-12
**Fase:** solo `.specify/` — sin cambios C#/TSX.

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

---

## Phase 1: Guards de jerarquía (D1/D2) — VERIFICADO, sin código nuevo

- [x] **Task COA-1.1: Leaf terminal (`parent_is_not_group`)**
  - Evidencia: `AccountValidator.EnsureValidParent` + `AccountTreeTable` (add-child solo si isGroup).
  - Escenarios: COA-G1. · Estado: IMPLEMENTADO.
- [x] **Task COA-1.2: Company + anti-self + herencia RootType**
  - Evidencia: `EnsureValidParent` (`parent_not_in_same_company`, `parent_is_self`, `root_type_mismatch`).
  - Escenarios: COA-G2, COA-G7, COA-G8. · Estado: IMPLEMENTADO.
- [x] **Task COA-1.3: Aciclicidad (`cycle_detected`)**
  - Evidencia: `EnsureNoCycle` + `GetByIdWithAncestorsAsync` en create/update.
  - Escenarios: COA-G2, COA-G3. · Estado: IMPLEMENTADO.

## Phase 2: Conversión grupo↔hoja + concurrencia (D3/D6) — VERIFICADO

- [x] **Task COA-2.1: Guard `EnsureValidIsGroupChange`**
  - Evidencia: `HasChildrenAsync`/`HasGlEntriesAsync` → `group_has_children` / `account_has_transactions`.
  - Escenarios: COA-G4, COA-G5. · Estado: IMPLEMENTADO.
- [x] **Task COA-2.2: RowVersion gate → 409 + reload UI**
  - Evidencia: `UpdateAccountCommandHandler` + `AccountsController.Update` + banner 409.
  - Escenarios: COA-G6. · Estado: IMPLEMENTADO.
- [x] **Task COA-2.3: Mapeo RFC 7807 completo**
  - Evidencia: spec.md §5 (duplicate→409, concurrency→409, resto→400, not_found→404).
  - Estado: IMPLEMENTADO (Create + Update; `ProducesResponseType` exhaustivo).

## Phase 3: Decisiones de scoping certificadas (D4/D5/FX) — CONGELADO

- [x] **Task COA-3.1: `balance_must_be` como posting-time check**
  - Evidencia: `ExpectedBalanceSideFor`/`EnsureBalanceSideConsistent`; hook GL como follow-up del contexto GeneralLedger (no bloquea).
- [x] **Task COA-3.2: Sin columnas `freeze_account` / `report_type`**
  - Evidencia: `Company.FrozenAccountsDate` (AC-04) + reportes derivados por RootType. Decisión COA-06.
- [x] **Task COA-3.3: FX sigue DEFERRED a R-14**
  - Ningún COA-0x toca settlement/varianza; `CurrencyId` = FK sin semántica FX.

## Phase 4: Certificación adversarial (Pass 3) — APROBADA

- [x] **Task COA-4.1: Consistencia con AC-03/AC-02/AC-04** — doble cerrojo leaf (COA-01+AC-03), GL append-only intacto, freeze reutilizado. Sin contradicciones.
- [x] **Task COA-4.2: Constitution Arts I/II/III/IV/VI/VII** — validador puro, TenantId inmutable, ledger intacto, índices/temporal, ProblemDetails, feature-driven + axios + testIDs.
- [x] **Task COA-4.3: Triad escrito** — `spec.md`, `plan.md`, `tasks.md` en `.specify/modules/coa-hardening/`, cada uno con `STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)`.

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**
