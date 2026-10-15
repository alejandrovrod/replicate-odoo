# Technical Plan: Plan de Cuentas (COA) Hardening

**Module:** `01-accounting / COA`
**Status:** CERTIFIED
**Version:** 1.0.0
**Date:** 2026-10-12
**Stack:** .NET (.NET 9/10), EF Core 9, SQL Server 2025, React 19 + TypeScript
**Standard:** Clean Architecture, DDD, CQRS, Tenant Isolation (constitution.md Arts I, II, IV, VI, VII)

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

---

## 1. Topology (sin cambios — certificación del estado existente)

```
Erp.Domain/
├── Entities/
│   ├── Account.cs              # RootType/Type/IsGroup/ParentAccountId/CurrencyId/RowVersion
│   ├── AccountValidator.cs     # EnsureValidFields | EnsureValidParent | EnsureNoCycle
│   │                           # | EnsureValidIsGroupChange | EnsureSubtreeRootTypeConsistent
│   │                           # | ExpectedBalanceSideFor / EnsureBalanceSideConsistent
│   ├── AccountBalanceSide.cs   # (enum inline en AccountValidator.cs) Debit | Credit
│   └── AccountErrorCodes.cs    # 17 códigos estables (ver spec.md §5)
├── Repositories/
│   └── IAccountRepository.cs   # GetByIdWithAncestorsAsync + HasChildrenAsync + HasGlEntriesAsync
Erp.Application/Features/Accounts/
├── Commands/
│   ├── CreateAccountCommand{Handler}.cs  # DefaultTypeFor + ancestor-walk + ExistsByCodeAsync
│   └── UpdateAccountCommand{Handler}.cs  # RowVersion gate + IsGroup guard + reparent sin cascada
└── Queries/GetAccountTreeQuery.cs        # Lectura jerárquica O(N)
Erp.Api/Controllers/V1/AccountsController.cs  # RFC 7807: duplicate→409, concurrency→409, resto→400, not_found→404
Frontend erp-client/src/features/accounting/
├── AccountTreeTable.tsx   # badges RootType+AccountType, add-child solo si isGroup, banner 409 + reload
├── api/updateAccountApi.ts # propaga rowVersion
└── AccountFormModal.tsx   # testIDs + validación parent group
```

Art I: `AccountValidator` permanece puro (cero deps, sin EF). Los checks con acceso a datos (`HasChildrenAsync`, `HasGlEntriesAsync`, `GetByIdWithAncestorsAsync`) viven en handlers y pasan booleanos/IDs al dominio.

## 2. CQRS Contracts (congelados por este triad)

```csharp
// Create (Type opcional → DefaultTypeFor)
public sealed record CreateAccountCommand(
    Guid CompanyId,
    string AccountCode,      // required, ≤50, único por (TenantId, CompanyId)
    string AccountName,      // required, ≤150
    AccountRootType RootType, // Asset | Liability | Equity | Income | Expense
    AccountType? Type,        // null ⇒ DefaultTypeFor: Equity→Equity, Income→Revenue,
                              // Expense→Expense, Asset/Liability→Other
    bool IsGroup,
    Guid? ParentAccountId,    // null = raíz; si set: group + misma company + mismo RootType + acíclico
    Guid? CurrencyId,         // FK catálogo; existencia validada (sin lógica FX — R-14)
    bool IsActive
) : ICommand<Result<AccountDto>>;

// Update (inmutables: TenantId, CompanyId, AccountCode, RootType, Type)
public sealed record UpdateAccountCommand(
    Guid Id,
    Guid CompanyId,           // debe coincidir o ⇒ account_not_found (404)
    string AccountName,
    bool? IsGroup,            // cambio ⇒ guard HasChildren/HasGlEntries
    bool ChangeParent,        // true ⇒ ParentAccountId aplica (null = mover a raíz)
    Guid? ParentAccountId,
    bool IsActive,
    byte[] RowVersion         // requerido; mismatch ⇒ concurrency_conflict (409)
) : ICommand<Result<AccountDto>>;
```

```csharp
// IAccountRepository — superficie COA (existente, sin cambios)
Task<IReadOnlyList<Account>> GetByIdWithAncestorsAsync(Guid accountId, CancellationToken ct = default);
//  contrato: ancestors[0] == el parent/entidad pedida (self-first); vacío = no existe.
Task<bool> HasChildrenAsync(Guid accountId, CancellationToken ct = default);
Task<bool> HasGlEntriesAsync(Guid accountId, CancellationToken ct = default);
```

## 3. Reglas de validación → código (trazabilidad)

| Regla | Dominio puro | Handler (datos) |
|---|---|---|
| Campos (code/name/root/type/company) | `EnsureValidFields` | — |
| Parent group + misma company + anti-self + RootType igual | `EnsureValidParent(candidate, parent)` | ancestor-walk `GetByIdWithAncestorsAsync` |
| Aciclicidad (candidate-en-cadena + loop) | `EnsureNoCycle(candidateId, ancestorIds)` | idem |
| Conversión grupo↔hoja | `EnsureValidIsGroupChange(cur, @new, hasChildren, hasGlEntries)` | `HasChildrenAsync` + `HasGlEntriesAsync` |
| Consistencia subárbol (futura cascada) | `EnsureSubtreeRootTypeConsistent` | — (hoy: rechazo cross-RootType, sin cascada) |
| `balance_must_be` posting-time | `ExpectedBalanceSideFor` + `EnsureBalanceSideConsistent` | hook futuro del motor GL (DEFERRED, no-entity) |

`DefaultTypeFor` (Application): Equity→Equity, Income→Revenue, Expense→Expense, resto→Other. Alineado con el DEFAULT SQL/backfill de la migración `AddAccountTypeAndUniqueCode`.

## 4. Persistencia (sin DDL nuevo — Art IV)

- `Account` temporal (`SYSTEM_VERSIONING = ON`), `UQ (TenantId, CompanyId, AccountCode)`, `FK_Account_Parent` self-ref, `RowVersion` para concurrencia optimista.
- Índices liderados por `TenantId`; `GLEntry` append-only intacto (ninguna regla COA escribe GL).
- No se añaden columnas `freeze_account` / `report_type` (decisión COA-06 certificada).

## 5. API — mapeo RFC 7807 (Art VI, congelado)

`POST /api/v1/accounts` → 201 / 400 / 409 (`duplicate_account_code`).
`PUT /api/v1/accounts/{id}` → 200 / 400 / 404 (`account_not_found`) / 409 (`concurrency_conflict`).
`GET /api/v1/accounts/tree?companyId=` → 200 / 400. Sin `[IdempotencyKeyRequired]` (no es posteo ledger — Art VI.4). Tabla completa en spec.md §5.

## 6. Frontend (Art VII, congelado)

`AccountTreeTable`: badges RootType+AccountType, add-child solo si `isGroup`, testIDs estables, banner 409 con reload. `updateAccountApi`: propaga `rowVersion` en cada update. `AccountFormModal`: testIDs + validación parent-group en cliente (defensa UX; la autoridad es el dominio).

## 7. Follow-ups fuera de este triad (no bloquean certificación)

1. **Hook `balance_must_be` en el motor GL** — llamar `EnsureBalanceSideConsistent` con el neto computado antes de append. Owner: contexto GeneralLedger.
2. **FX (R-14)** — alcance intacto DEFERRED; `CurrencyId` no adquiere semántica cambiaria aquí.

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**
