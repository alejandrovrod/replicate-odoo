# Tasks — Bank Catalog

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

Orden Dominio → Infra/Seeding → App → Api → UI. Cada task es atómica, compilable y testeable. Sin regresión: `BankName` fallback/display, `BankId` nullable Restrict.

- [ ] **T1 Dominio — `Bank` + errores + `Company.CountryCode`** · Crear `Entities/Bank.cs` (`ITenantEntity`, `CountryCode(2)`, `Code(20)`, `Name(150)`, `SwiftBic(11)?`, `IsActive`); agregar `Company.CountryCode string?(2)`; agregar `BankAccount.BankId Guid?` + navegación `Bank?`; `BankingErrorCodes.BankNotFound/InvalidBankCountry`. Test: unicidad conceptual + `TenantId` inmutable. _Cierra D3/D4._
- [ ] **T2 Infra — DDL + configuración EF** · `Configurations/BankConfiguration.cs` (temporal `BanksHistory`, `UQ(Tenant,Country,Code)`, índice `(TenantId,CountryCode,IsActive)`); `CompanyConfiguration` += `CountryCode NULL`; `BankAccountConfiguration` += `BankId NULL FK Restrict`; migración `Add_BankCatalog_And_CompanyCountry`. Test: aplica en SQL local, índice lidera con `TenantId`. _Cierra D3._
- [ ] **T3 Infra — Seeding MX idempotente** · `Data/Seed/banks_MX.json` (10 bancos del spec §5) + `BankCatalogSeeder` (skip-if-exists por `(TenantId,Country,Code)`). Test: doble seed sin duplicados (BNK-G4). _Cierra D2-G4._
- [ ] **T4 Dominio/Infra — Repositorio** · `IBankRepository.GetBanksByCountryAsync(countryCode, search, paging)` + `GetBankByIdAsync`; impl en `BankRepository` (sin `Where(TenantId)` manual — query filter Art II). Test: tenant A no ve bancos de B; `search=012` devuelve BBVA (BNK-G1/G2). _Cierra D4._
- [ ] **T5 App — CQRS** · `BankDto`; `GetBanksQuery(CountryCode?, Search?, Page, PageSize)` + handler (país default `Company.CountryCode ?? "MX"`, país inválido → `InvalidBankCountry`); `Create/UpdateBankAccountCommand` += `Guid? BankId = null` + validación (existe+activo+mismo país sino `BankNotFound`; si null exige `BankName`). Tests: BNK-G3 (create sin `bankId` → 201-equivalente), `bankId` ajeno/inactivo → `bank_not_found`, país `XX` → `invalid_bank_country` (BNK-G5). _Cierra D4._
- [ ] **T6 Api — `BanksController` + mapeo RFC7807** · `GET /api/v1/banks` (200 / 400 `invalid_bank_country`), `GET /api/v1/banks/{id}` (200 / 404 `bank_not_found`), Art VI completo; `BankAccountsController.Create`: `bank_not_found → 400`. Tests integración: BNK-G1/G2/G5 + create con `bankId` inexistente → 400. _Cierra D4._
- [ ] **T7 UI — Catálogo en el formulario** · `types.ts` += `BankDto`; `api/useBankAccounts.ts` += `getBanksApi`; `components/BankCombobox.tsx` (autocomplete código/nombre, fallback a `Input` libre si vacío/falla); `BankAccountFormModal.tsx`: `BankCombobox` en lugar del `Input bankName` libre + modo fallback. Test manual: autocomplete `012` → BBVA; sin catálogo → alta manual válida. _Cierra D2-G2/G3._
- [ ] **T8 Regresión — imports/reconciliación intactos** · Suite banking existente en verde (`BankStatementImport`, reglas, reconciliación, `BankAccountGuards`, `RequirePostableGlAccountAsync`); test extra: cuenta con `BankId` importa y concilia igual que con `BankName` libre. _Cierra D6._

## Pass 3 — Nota adversarial

Ninguna task rompe el contrato actual: parámetros nuevos opcionales al final, columnas nuevas nullables, FK `NO ACTION`, `BankName` siempre poblado. El orden T1→T8 garantiza que ningún paso quede sin compilar.
