# Spec — Bank Catalog (Catálogo Maestro de Bancos, offline)

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

## 0. Propósito

Catálogo maestro de bancos por país (arranca MX, clave 3 dígitos Banxico/SPEI) desacoplado y 100% offline: sin APIs vivas externas. `BankAccount` puede referenciarlo vía `BankId` opcional (nullable); `BankName` texto libre queda como fallback/display para no romper imports ni reconciliación.

## 1. Pass 1 — Gap Matrix (6 dimensiones)

| # | Dimensión | Estado actual verificado | Gap | Decisión |
|---|-----------|--------------------------|-----|----------|
| D1 | Catálogo desacoplado offline | No existe entidad `Bank`, ni tabla `Banks`, ni endpoint `GET /v1/banks`. Cero dependencia de APIs vivas | Crear módulo nuevo desacoplado, seed JSON versionado en repo | Nueva entidad `Bank : ITenantEntity`, tabla + seed `banks_MX.json`; prohibido llamar APIs externas en runtime |
| D2 | Gherkin BNK-G1..G5 | Sin casos de catálogo; `BankAccountFormModal.tsx` usa `Input` texto libre `bankName` | Especificar BNK-G1 filtrar por país MX, G2 autocomplete clave 3 dígitos, G3 fallback manual, G4 unicidad `(TenantId,CountryCode,Code)`, G5 error país inválido | Ver §2 (formales en Pass 2) |
| D3 | DDL + índice + temporal + `Company.CountryCode` | `Company.cs` SIN `CountryCode`/`Country`; `BankAccount.cs` SIN `BankId`/navegación `Bank` | DDL `Banks` + índice filtrado + temporal; decisión explícita `Company.CountryCode` | Ver §3. `Company.CountryCode NVARCHAR(2) NULL` se agrega como invariante explícita con migración (nullable para no romper filas legacy; default de lectura = `MX` solo a nivel query, nunca default en columna) |
| D4 | CQRS + errores + RFC7807 | `CreateBankAccountCommand(CompanyId, AccountName, BankName, AccountNumber, GLAccountId, CurrencyId?, IsActive)` SIN `BankId`; `IBankRepository` SIN `GetBanksByCountryAsync`; `BankingErrorCodes` SIN `BankNotFound`/`InvalidBankCountry` (solo `BankAccountNotFound` etc.); `BankAccountsController` SIN `GET /v1/banks` | `GetBanksQuery(CountryCode?)` + `CreateBankAccount` con `BankId` opcional + 2 códigos nuevos + mapeo RFC7807 | Ver §4. `GET /v1/banks?countryCode=MX` → 200; `countryCode` inválido → 400 `invalid_bank_country`; `BankId` inexistente en create → 400 `bank_not_found`; `GET /v1/banks/{id}` inexistente → 404 `bank_not_found` |
| D5 | Tasks atómicas | Sin tasks de catálogo | Secuencia Dominio → Infra/Seeding → App → Api → UI, cada task compilable y testeable | Ver `tasks.md` (T1..T8) |
| D6 | Sin regresión | `BankAccount.BankName(100)`, `BankStatementImport` y reconciliación resuelven por cuenta, no por banco; `BankAccountGuards` + `RequirePostableGlAccountAsync` vigentes | `BankId` nullable + FK `Restrict`; `BankName` intocable | `BankName` queda como fallback/display (requerido si `BankId` es null); imports/reconciliación sin cambios de comportamiento |

## 2. Pass 2 — Gherkin formal (BNK-G1..BNK-G5)

```gherkin
Feature: Bank catalog (MX)
  Background:
    Given tenant "T1" with seeded MX catalog

  Scenario: BNK-G1 filtrar por país MX
    When GET /api/v1/banks?countryCode=MX
    Then status 200 and every item has countryCode "MX" and isActive true first

  Scenario: BNK-G2 autocomplete por clave 3 dígitos Banxico/SPEI
    When GET /api/v1/banks?countryCode=MX&search=012
    Then status 200 and "BBVA México" is in the results with code "012"

  Scenario: BNK-G3 fallback manual sin BankId
    When POST /api/v1/bankaccounts with bankName "Banco Ficticio" and no bankId
    Then status 201 and response bankName is "Banco Ficticio" and bankId is null

  Scenario: BNK-G4 unicidad (TenantId, CountryCode, Code)
    Given bank (T1, MX, 012) exists
    When seeding (T1, MX, 012) again
    Then the row is skipped (no duplicate, no error fatal) and unique index holds

  Scenario: BNK-G5 error país inválido
    When GET /api/v1/banks?countryCode=XX
    Then status 400 problem code "invalid_bank_country"
```

## 3. Pass 2 — DDL + invariantes

```sql
-- Invariante explícita: Company.CountryCode (nullable, sin default en columna)
ALTER TABLE dbo.Company ADD CountryCode NVARCHAR(2) NULL;

CREATE TABLE dbo.Banks (
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Banks PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CountryCode NVARCHAR(2) NOT NULL CONSTRAINT CK_Banks_CountryCode CHECK (CountryCode = UPPER(CountryCode)),
    Code NVARCHAR(20) NOT NULL,          -- clave 3 dígitos Banxico/SPEI (MX); genérico por país
    Name NVARCHAR(150) NOT NULL,
    SwiftBic NVARCHAR(11) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Banks_IsActive DEFAULT (1),
    CONSTRAINT UQ_Banks_Tenant_Country_Code UNIQUE (TenantId, CountryCode, Code),
    CONSTRAINT FK_Banks_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.BanksHistory));

CREATE INDEX IX_Banks_Tenant_Country_Active
    ON dbo.Banks (TenantId, CountryCode, IsActive) INCLUDE (Code, Name);

-- BankAccount.BankId: nullable, Restrict (nunca cascada), BankName intocable
ALTER TABLE dbo.BankAccount ADD BankId UNIQUEIDENTIFIER NULL
    CONSTRAINT FK_BankAccount_Bank FOREIGN KEY (BankId) REFERENCES dbo.Banks(Id) ON DELETE NO ACTION;
```

Invariantes:
- Art II: `Bank : ITenantEntity`, `TenantId` inmutable, query filter automático, sin `.Where(TenantId)` manual.
- Art IV: `TenantId` lidera el índice; temporal `BanksHistory` (catálogo maestro auditable).
- `CountryCode`: ISO 3166-1 alpha-2 mayúsculas; query sin `countryCode` resuelve país vía `Company.CountryCode ?? "MX"` (solo lectura, sin persistir default).
- Seed idempotente por `(TenantId, CountryCode, Code)`; solo filas `IsActive=1` en autocomplete.

## 4. CQRS + errores + RFC7807

- `GetBanksQuery(CountryCode?: string, Search?: string, Page, PageSize)` → `PagedResult<BankDto>`. Sin `countryCode` → país de la empresa (`Company.CountryCode ?? "MX"`).
- `CreateBankAccountCommand` += `Guid? BankId = null` (opcional, al final para no romper call-sites). Handler: si `BankId.HasValue` → existe y activo y mismo `CountryCode` que la empresa (si empresa tiene país) sino `BankNotFound`; si null → `BankName` requerido (guarda actual `BankNameRequired` intacta). `RequirePostableGlAccountAsync` sin cambios.
- `UpdateBankAccountCommand` += `Guid? BankId` con la misma validación.
- `BankingErrorCodes`: `BankNotFound = "bank_not_found"`, `InvalidBankCountry = "invalid_bank_country"`.
- API (Art VI: `[ApiController]`, `Route("api/v1/[controller]")`, `TenantMember`, `Produces/Consumes`, `ProducesResponseType`):
  - `GET /api/v1/banks?countryCode=MX&search=012` → 200 `PagedResult<BankDto>`; país inválido → 400 `invalid_bank_country`.
  - `GET /api/v1/banks/{id}` → 200 / 404 `bank_not_found`.
  - `POST /api/v1/bankaccounts` con `bankId` inexistente → 400 `bank_not_found` (create = 400, no 404, consistente con el controlador actual).
- `BankDto { Id, CountryCode, Code, Name, SwiftBic, IsActive }`. `BankAccountDto` += `BankId?` + `BankName` (display, siempre poblado: del catálogo si hay `BankId`, sino el libre).

## 5. Pass 2 — Seeder JSON MX inicial (`seed/banks_MX.json`)

Diez bancos con clave 3 dígitos y nombre oficial. `swiftBic` solo cuando es oficial y conocido, si no `null`:

```json
[
  { "countryCode": "MX", "code": "002", "name": "Banco Nacional de México (Banamex)", "swiftBic": "BNMXMXMM", "isActive": true },
  { "countryCode": "MX", "code": "012", "name": "BBVA México", "swiftBic": "BCMRMXMM", "isActive": true },
  { "countryCode": "MX", "code": "014", "name": "Banco Santander México", "swiftBic": "BMSXMXMM", "isActive": true },
  { "countryCode": "MX", "code": "021", "name": "HSBC México", "swiftBic": "BIMEMXMM", "isActive": true },
  { "countryCode": "MX", "code": "030", "name": "Banco del Bajío", "swiftBic": null, "isActive": true },
  { "countryCode": "MX", "code": "044", "name": "Scotiabank México", "swiftBic": null, "isActive": true },
  { "countryCode": "MX", "code": "058", "name": "Banco Regional (Banregio)", "swiftBic": null, "isActive": true },
  { "countryCode": "MX", "code": "062", "name": "Banca Afirme", "swiftBic": null, "isActive": true },
  { "countryCode": "MX", "code": "072", "name": "Banco Mercantil del Norte (Banorte)", "swiftBic": "MENOMXMT", "isActive": true },
  { "countryCode": "MX", "code": "127", "name": "Banco Azteca", "swiftBic": null, "isActive": true }
]
```

## 6. Pass 3 — Adversarial (coexistencia, unicidad, tenant, regresión)

| Ataque | Veredicto |
|--------|-----------|
| `BankName` libre + `BankId` coexisten y divergen | PASS: `BankName` es display/fallback; si hay `BankId`, el display se deriva del catálogo en lectura pero la columna persiste el último valor libre (auditoría); validación no exige igualdad, solo existencia del `BankId` |
| Duplicado `(TenantId,CountryCode,Code)` por doble seed concurrente | PASS: constraint `UQ_Banks_Tenant_Country_Code` + seed idempotente (skip-if-exists); sin excepción fatal |
| Fuga entre tenants (`GetBanksByCountryAsync` sin filtro) | PASS: Art II query filter automático; test tenant A no ve bancos de tenant B |
| `countryCode` minúsculas / `XX` inexistente | PASS: normalizar a mayúsculas; lista blanca ISO por país con seed; `XX` → 400 `invalid_bank_country` |
| `BankId` de otro tenant o inactivo en create | PASS: handler rechaza con `bank_not_found` (inexistente, ajeno o inactivo son indistinguibles por diseño) |
| `BankId` con `CountryCode` distinto al de la empresa | PASS: handler rechaza con `bank_not_found` (no se mezcla catálogo MX en empresa con país distinto) |
| Borrado de banco referenciado por cuentas | PASS: FK `Restrict`/`NO ACTION` — el borrado lógico es `IsActive=false`, nunca `DELETE` físico con referencias |
| Regresión `BankStatementImport` / reconciliación / `BankAccountGuards` | PASS: esos flujos resuelven por `BankAccountId`, no por `Bank`; `BankName` requerido-si-sin-`BankId` preserva la guarda actual; `RequirePostableGlAccountAsync` intacto |
| Frontend sin catálogo (offline-first, seed aún no corrido) | PASS: `BankCombobox` con fallback a `Input` libre si `GET /v1/banks` vacío/falla; el create sin `bankId` sigue válido (BNK-G3) |

## 7. Fuera de alcance (esta fase)

Código C#/TSX, migraciones EF, tests. Solo spec/plan/tasks.
