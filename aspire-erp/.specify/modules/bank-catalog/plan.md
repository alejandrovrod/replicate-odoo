# Plan — Bank Catalog

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

## Pass 1 — Gap Matrix (diseño que cierra los 6 gaps del spec)

| Gap | Diseño |
|-----|--------|
| D1 offline desacoplado | Entidad `Bank` en `Erp.Domain`, `BankConfiguration` EF en Infra, seed JSON embebido; cero HttpClient / APIs vivas |
| D2 BNK-G1..G5 | `GetBanksQuery` con `countryCode+search` paginado + validación país; create con `BankId?` + fallback manual |
| D3 DDL/temporal/`Company.CountryCode` | Migración 1: `Banks` temporal + índice filtrado + `Company.CountryCode NULL` + `BankAccount.BankId NULL Restrict` |
| D4 CQRS/errores/RFC7807 | `BankDto`, `GetBanksQueryHandler`, `Create/UpdateBankAccount` + `BankId?`, `BankNotFound`/`InvalidBankCountry`, `BanksController` Art VI |
| D5 tasks atómicas | T1..T8 en `tasks.md`, orden Dominio → Infra/Seeding → App → Api → UI |
| D6 sin regresión | `BankId` nullable al final de los records, `BankName` intacto, imports/reconciliación sin tocar |

## Arquitectura (Clean, Art I)

- **Domain** (`Erp.Domain`, cero deps): `Entities/Bank.cs` (`ITenantEntity`: `Id` GUID, `TenantId`, `CountryCode(2)`, `Code(20)`, `Name(150)`, `SwiftBic(11)?`, `IsActive`, sin `RowVersion` — catálogo maestro con temporal basta); `BankingErrorCodes.BankNotFound/InvalidBankCountry`; `Company.CountryCode NVARCHAR(2)?`.
- **Application**: `DTOs/BankDto.cs`; `Features/Banking/Queries/GetBanksQuery{ CountryCode?, Search?, Page, PageSize }` + handler (resuelve país vía empresa, valida ISO, pagina); `CreateBankAccountCommand`/`UpdateBankAccountCommand` += `Guid? BankId = null` + validación (existe+activo+mismo país) + `BankName` requerido si null; `IBankRepository.GetBanksByCountryAsync + GetBankByIdAsync`.
- **Infrastructure**: `Configurations/BankConfiguration.cs` (temporal, `UQ(Tenant,Country,Code)`, índice `(TenantId,CountryCode,IsActive)`); `BankRepository` impl; `Data/Seed/banks_MX.json` (10 bancos §5 del spec) + `BankCatalogSeeder` idempotente; `CompanyConfiguration` += `CountryCode(2) NULL`.
- **Api**: `Controllers/V1/BanksController.cs` Art VI (`GET /api/v1/banks`, `GET /api/v1/banks/{id}`); `BankAccountsController.Create` mapea `bank_not_found → 400` (sin cambiar su forma actual).
- **UI** (Art VII, `features/banking/`): `types.ts` += `BankDto`; `api/useBankAccounts.ts` += `getBanksApi(countryCode, search)`; `components/BankCombobox.tsx` nuevo (autocomplete por código/nombre + fallback a `Input` libre); `BankAccountFormModal.tsx`: reemplaza `Input bankName` por `BankCombobox` + campo libre solo en modo fallback; axios centralizado + ProblemDetails intactos.

## Migraciones (orden)

1. `Add_BankCatalog_And_CompanyCountry`: crea `Banks` (+history), `Company.CountryCode`, `BankAccount.BankId` FK Restrict, índices.
2. Seed MX (10 filas por tenant provisionado, idempotente, fuera de la migración transaccional si el provisionador lo exige).

## Pass 3 — Riesgos adversariales y mitigación

Coexistencia display/libre, unicidad bajo doble seed, tenant isolation por query filter, país inválido → 400, `BankId` ajeno/inactivo/país-distinto → `bank_not_found`, borrado referenciado bloqueado por Restrict + baja lógica, imports/reconciliación intactos, UI offline-first con fallback. Detalle en `spec.md` §6.
