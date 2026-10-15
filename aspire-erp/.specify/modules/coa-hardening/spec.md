# Functional Specification: Plan de Cuentas (COA) Hardening

**Module:** `01-accounting / COA` (sub-scope)
**Status:** CERTIFIED
**Version:** 1.0.0
**Date:** 2026-10-12
**Methodology:** spec-recursive-validator — 3-Attempt Convergence Loop (Forensic Audit → Surgical Patch → Adversarial Certification)
**Parent spec:** `.specify/specs/01-accounting/spec.md` v2.1.0 (AC-01..AC-04 + AC-06/AC-07 in scope, AC-05 FX DEFERRED to R-14)
**Constitution:** `.specify/constitution.md` (Art I, II, III, IV, VI, VII)
**Domain rules:** `.specify/domain_business_rules_ddd.md` §1 + §3.4

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

---

## Pass 1 — Forensic Audit & Gap Matrix (6 dimensions, foco COA)

Evidence base: `Erp.Domain/Entities/AccountValidator.cs` (EnsureValidFields, EnsureValidParent, EnsureNoCycle, EnsureValidIsGroupChange, EnsureSubtreeRootTypeConsistent, ExpectedBalanceSideFor/EnsureBalanceSideConsistent + `AccountBalanceSide`), `Account.cs` (RootType/Type/IsGroup/ParentAccountId/CurrencyId/RowVersion), `IAccountRepository` (HasChildrenAsync/HasGlEntriesAsync, GetByIdWithAncestorsAsync), `UpdateAccountCommandHandler` (RowVersion gate + reparent sin cascada), `CreateAccountCommandHandler` (DefaultTypeFor), `AccountErrorCodes`, `AccountsController` (RFC 7807 mapping), `AccountTreeTable.tsx` + `updateAccountApi.ts` + `AccountFormModal`.

| # | Dimensión | Veredicto | Evidencia |
|---|-----------|-----------|-----------|
| D1 | Hoja terminal (leaf-only) | **IMPLEMENTADO** | `EnsureValidParent` rechaza parent `!IsGroup` → `parent_is_not_group`; GL solo leaf por AC-03 + `EnsureValidIsGroupChange` leaf→group con GL → `account_has_transactions`. Frontend: add-child solo si `isGroup`. |
| D2 | Herencia RootType + anti-ciclos | **IMPLEMENTADO** | `EnsureValidParent` exige misma company (`parent_not_in_same_company`), anti-self (`parent_is_self`), igualdad RootType (`root_type_mismatch`); `EnsureNoCycle` rechaza candidate-en-cadena + loop persistido (`cycle_detected`); reparent vía `GetByIdWithAncestorsAsync`, rechazo cross-RootType sin cascada; `EnsureSubtreeRootTypeConsistent` como check puro para futura cascada. |
| D3 | Conversión grupo↔hoja con GL | **IMPLEMENTADO** | `EnsureValidIsGroupChange(current,new,hasChildren,hasGlEntries)`: grupo→hoja con hijos → `group_has_children`; hoja→grupo con GL → `account_has_transactions`; flags resueltos por `HasChildrenAsync`/`HasGlEntriesAsync` (dominio puro, Art I). |
| D4 | `balance_must_be` | **IMPLEMENTADO como check posting-time (NO invariante de entidad)** — correcto | `ExpectedBalanceSideFor` (Debit: Bank/Cash/Receivable/Stock/COGS/Expense/Depreciation; Credit: Payable/Tax/Equity/Revenue; null = unconstrained Other/RoundOff) + `EnsureBalanceSideConsistent(type, netDebitMinusCredit)`, cero siempre válido. Decisión de auditoría: `Account` no persiste balance ⇒ no hay invariante de entidad que enforzar hoy; el mapeo es source-of-truth para el futuro hook del motor GL. `balance_side_mismatch` reservado para ese hook. No es GAP, es scoping correcto. |
| D5 | `freeze_account` / `report_type` | **IMPLEMENTADO por diseño existente / DERIVADO — sin columna** — correcto | `freeze_account` ≙ `Company.FrozenAccountsDate` existente (AC-04, `FiscalPeriodLockedException`); columna por-cuenta innecesaria y contradictoria con el lock a nivel compañía. `report_type` ≙ derivado BS/P&L/TB de RootType (queries `GetTrialBalance/BalanceSheet/ProfitAndLoss`), no columna persistida — persistirlo violaría single-source-of-truth. No son GAPs. |
| D6 | Concurrencia + API + frontend + índices | **IMPLEMENTADO** | RowVersion gate en Update → `ConcurrencyConflict` → HTTP 409 (`AccountsController.Update`); `duplicate_account_code` → 409, resto dominio → 400, `account_not_found` → 404 (Art VI ProblemDetails). Frontend: badges RootType+AccountType, add-child solo si isGroup, testIDs, banner 409 con reload; `updateAccountApi.ts` propaga rowVersion. ĐH TenantId leading + temporal table por Art II/IV (plan heredado 01-accounting). |

### FX scope guard

AC-05 (Realized FX) sigue **DEFERRED a R-14**. Este triad no lo mueve: ningún invariante COA-0x depende de moneda/settlement; `CurrencyId` es FK a catálogo (validada por existencia en Create) sin lógica FX.

### GAPS encontrados (ninguno bloqueante)

1. ~~Faltan guards de jerarquía~~ → ya implementado (D1/D2).
2. ~~Falta gate de conversión con GL~~ → ya implementado (D3).
3. `balance_must_be` no cableado al pipeline de posteo GL → **DEFERRED intencional** (el motor GL llamará `EnsureBalanceSideConsistent` con el balance neto computado; trackear como follow-up del contexto GL, no de este triad).
4. `ConcurrencyErrorCodes.ConcurrencyConflict` vive fuera de `AccountErrorCodes` → documentado en el mapeo RFC 7807 de este spec (§5), sin cambio de código en esta fase.

**Conclusión Pass 1:** 0 gaps bloqueantes. El triad certifica el estado implementado y congela las decisiones de scoping (D4/D5/FX).

---

## Pass 2 — Invariantes COA (surgical patch: lo que el triad congela)

### COA-01: Leaf terminal — ningún hijo bajo cuenta detalle
Parent debe existir, ser `IsGroup == true`, misma company, distinto Id.

### COA-02: Herencia RootType estricta
`child.RootType == parent.RootType`, en create y en cada reparent. Cross-RootType ⇒ rechazo, sin cascada.

### COA-03: Aciclicidad del árbol
La cadena de ancestros desde el parent propuesto hasta la raíz no debe contener al candidato ni loops internos.

### COA-04: Conversión grupo↔hoja custodiada por GL
Grupo→hoja con hijos ⇒ `group_has_children`. Hoja→grupo con GL ⇒ `account_has_transactions`. Sin hijos/GL ⇒ conversión libre.

### COA-05: `balance_must_be` es check posting-time, no invariante de entidad
Mapeo §4 de este spec; el motor GL lo invocará con balance neto. Cero siempre válido; `Other`/`RoundOff` unconstrained.

### COA-06: Sin columnas `freeze_account` / `report_type`
Freeze = `Company.FrozenAccountsDate` (AC-04). Report = derivado de RootType. Añadir columnas sería contradicción con el diseño certificado.

### COA-07: Concurrencia optimista + superficie RFC 7807
Update exige RowVersion coincidente; mismatch ⇒ 409 con recarga guiada en UI.

---

## 4. Gherkin — escenarios de aceptación COA

### COA-G1: Anidación bajo cuenta detalle → `parent_is_not_group`
- **Given** cuenta hoja `1110 - Cash` (`IsGroup = false`)
- **When** se crea (o mueve) una cuenta con `ParentAccountId = 1110`
- **Then** el comando falla con código `parent_is_not_group` → HTTP 400 ProblemDetails.

### COA-G2: Ciclo directo (self-parent) → `parent_is_self`
- **Given** cuenta `A`
- **When** update con `ParentAccountId = A.Id`
- **Then** falla con `parent_is_self` → 400, sin mutación.

### COA-G3: Ciclo indirecto (reparent bajo descendiente) → `cycle_detected`
- **Given** cadena `Root → A → B`
- **When** update de `A` con `ParentAccountId = B.Id`
- **Then** el ancestor-walk contiene a `A` ⇒ `cycle_detected` → 400, sin mutación.

### COA-G4: Conversión grupo→hoja con hijos → `group_has_children`
- **Given** grupo `1000 - Assets` con ≥1 hijo
- **When** update `IsGroup: true → false`
- **Then** falla con `group_has_children` → 400; el árbol queda intacto.

### COA-G5: Conversión hoja→grupo con GL → `account_has_transactions`
- **Given** hoja `5110 - Office Supplies` con ≥1 `GLEntry`
- **When** update `IsGroup: false → true`
- **Then** falla con `account_has_transactions` → 400; el historial ledger no queda oculto.

### COA-G6: Concurrencia RowVersion → 409 + reload
- **Given** dos sesiones leen la cuenta `v1` (mismo RowVersion)
- **When** sesión-1 guarda (→ `v2`) y sesión-2 guarda con `v1`
- **Then** sesión-2 recibe `concurrency_conflict` → HTTP 409; la UI muestra banner con reload y no reintenta a ciegas.

### COA-G7: RootType mismatch en reparent → `root_type_mismatch`
- **Given** cuenta `Expense` y parent candidato `Asset`
- **When** update con `ChangeParent` a ese parent
- **Then** falla con `root_type_mismatch` → 400; sin cascada de subárbol.

### COA-G8: Company mismatch → `parent_not_in_same_company`
- **Given** cuenta de `Company-A` y parent de `Company-B`
- **When** create/update con ese parent
- **Then** falla con `parent_not_in_same_company` → 400 (defensa en profundidad junto al filtro tenant/company).

---

## 5. Enumerado `AccountErrorCodes` completo + mapeo RFC 7807

| Código | Origen | HTTP |
|---|---|---|
| `company_required`, `account_code_required`, `account_code_too_long`, `account_name_required`, `account_name_too_long`, `invalid_root_type`, `invalid_account_type`, `currency_invalid` | `EnsureValidFields` / validación moneda | 400 |
| `parent_not_found` | handlers (ancestor-walk vacío) | 400 |
| `parent_is_self` | `EnsureValidParent` + gate update | 400 |
| `parent_not_in_same_company` | `EnsureValidParent` | 400 |
| `parent_is_not_group` | `EnsureValidParent` | 400 |
| `root_type_mismatch` | `EnsureValidParent` / `EnsureSubtreeRootTypeConsistent` | 400 |
| `cycle_detected` | `EnsureNoCycle` | 400 |
| `group_has_children` | `EnsureValidIsGroupChange` | 400 |
| `account_has_transactions` | `EnsureValidIsGroupChange` | 400 |
| `balance_side_mismatch` | `EnsureBalanceSideConsistent` (hook futuro GL) | 400 |
| `duplicate_account_code` | `ExistsByCodeAsync` | **409** |
| `concurrency_conflict` (`ConcurrencyErrorCodes`) | RowVersion gate | **409** |
| `account_not_found` | handlers (get / company mismatch) | **404** |

Controller: `AccountsController.Create` (duplicate→409, resto→400), `Update` (not_found→404, concurrency→409, resto→400). `type` del ProblemDetails: `rfc9110#section-15.5.10` para 409, `15.5.1` para el resto.

---

## Pass 3 — Adversarial Certification

- **Topología 01-accounting:** COA-01/COA-04 refuerzan AC-03 (GL solo leaf) sin contradicción: la guarda de conversión impide esconder posteos tras un grupo, y AC-03 impide postear en grupos. Doble cerrojo consistente.
- **GeneralLedger append-only (AC-02):** `account_has_transactions` consulta `HasGlEntriesAsync` en modo lectura; ninguna regla COA muta `GLEntry`. Sin contradicción.
- **Freeze (AC-04):** COA-06 reutiliza `FrozenAccountsDate`; el COA no introduce ventanas de bypass (create/update de cuentas no postean). Sin contradicción.
- **FX (AC-05 DEFERRED/R-14):** ningún COA-0x toca settlement ni varianza cambiaria. Alcance intacto.
- **Constitution:** Art I (validador puro, cero deps) ✓; Art II (TenantId inmutable, sin `Where` manual) ✓; Art III (partida doble + GL append-only intactos) ✓; Art IV (índices liderados por TenantId + temporales, sin DDL nuevo) ✓; Art VI (versionado + ProblemDetails + `ProducesResponseType` exhaustivo) ✓; Art VII (feature-driven + axios centralizado + testIDs) ✓.
- **Fase sin código:** este triad solo escribe `.specify/`; no modifica C#/TSX. ✓

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**
