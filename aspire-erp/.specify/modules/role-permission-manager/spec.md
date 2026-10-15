> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Spec: Role & Permission Manager

## 0. Estado verificado del repositorio (Pass 1 — evidencia, no invención)

- `Erp.Domain/Entities/Security/Role.cs`: `Id, TenantId, Name, Description, IsSystemDefault, Permissions ICollection<DocTypePermission>`. **NO** existe `IsSystemRole`; el flag canónico es `IsSystemDefault`.
- `DocTypePermission.cs`: `Id, TenantId, RoleId + nav, DocType string, CanRead/Write/Create/Delete/Submit/Cancel`. Implementa `ITenantEntity`.
- `User.cs` + `UserRole.cs`: `Id, TenantId, UserId + nav, RoleId + nav`. Implementa `ITenantEntity`.
- `SecurityRoleSeeder.cs`: 6 roles sistema (`System Manager` + Sales/Purchase/Accounting/HR/Manufacturing). `System Manager` **sin permission rows** — bypass total en handler (equivalente Administrator ERPNext). Roles funcionales con matriz por DocType (`Create => Write`, `Delete => false` salvo System Manager). Seed idempotente por `(TenantId, Name)` y `(TenantId, RoleId, DocType)`; converge en unique violation. Guids `SystemManagerRoleId` etc. son solo mapeo en memoria (los `Id` reales son `Guid.NewGuid` por tenant).
- `DocTypePermissionAuthorization.cs`: bypassa toda policy `permission:` si `IsInRole("System Manager")`; si no, exige claim `perms` = `doctype:action`.
- `JwtTokenGenerator.cs`: claims `sub/email/tenant_id` + `ClaimTypes.Role` y `roles` por cada `UserRole.Role.Name` + `perms` aplanados `doctype:action`, expira 24h. **SIN refresh/revocación server-side.**
- `AuthErrorCodes.cs`: códigos `UPPER_SNAKE` (`AUTH_INVALID_PASSWORD`, etc.); cada constante debe existir en `ErrorMessages{,.es}.resx` (enforced por `ErrorCatalogCoverageTests`) y espejarse a frontend `error.json` vía `scripts/i18n-sync.mjs`. **NO** existen `ROLE_NOT_FOUND` / `SYSTEM_MANAGER_IMMUTABLE` / `CANNOT_REMOVE_LAST_ADMIN` — este módulo los agrega.
- **NO** existe `IRoleRepository`, `RoleValidator`, `Features/Security` para roles (solo comandos MFA/login/password), ni `RolesController`, ni frontend `src/features/security/`.

## 1. Gap Matrix 6D (Pass 1)

| D | Dimensión | Gap | Decisión |
|---|-----------|-----|----------|
| D1 | Inmutabilidad System Manager + TenantId | Riesgo de eliminar/editar el rol raíz o mover `TenantId` | `System Manager` (`IsSystemDefault == true && Name == "System Manager"`): **no eliminable, matriz no editable (no tiene rows — cualquier `UpdatePermissions` sobre él → `SYSTEM_MANAGER_IMMUTABLE`), no removible del último admin (`CANNOT_REMOVE_LAST_ADMIN`)**. `TenantId` inmutable (Art II: `SaveChangesAsync` lanza `InvalidOperationException` ante alteración). `Assign/Remove` validan mismo tenant de usuario y rol. |
| D2 | Gherkin SEC-G1..G6 | Sin escenarios formales | Ver §2. Token: el cambio de roles/permisos **aplica en próximo login** (claims `roles`/`perms` se emiten al generar JWT); documentar como login-forzado / expiración 24h. **No inventar** infra de refresh tokens ni revocación server-side (inexistente en `JwtTokenGenerator`). |
| D3 | DDL + índices | Tablas existen, explicitar constraints | Ver §3. UQ `Roles(TenantId,Name)`, `DocTypePermissions(TenantId,RoleId,DocType)`, `UserRoles(TenantId,UserId,RoleId)`; índices compuestos con `TenantId` líder (Art IV). Sin migración destructiva: solo UQ/índices faltantes. |
| D4 | CQRS + errores RFC7807 | Sin comandos de roles | Ver §4. Comandos tipados + validadores FluentValidation. Nuevos `AuthErrorCodes`: `ROLE_NOT_FOUND` (404), `ROLE_NAME_CONFLICT` (409), `SYSTEM_MANAGER_IMMUTABLE` (403), `CANNOT_REMOVE_LAST_ADMIN` (409), `USER_ROLE_ALREADY_ASSIGNED` (409), `USER_ROLE_NOT_FOUND` (404), `CROSS_TENANT_ASSIGNMENT_DENIED` (403). Todos `UPPER_SNAKE` + resx EN/ES + espejo `error.json` vía `i18n-sync.mjs`. |
| D5 | Orden tasks | — | Dominio → App → Api → UI (ver `tasks.md`). |
| D6 | Compatibilidad | Regresión seeder/JWT/handler | `IsSystemDefault` (no `IsSystemRole`); System Manager sin rows; bypass `IsInRole("System Manager")` intacto; claims `roles`+`perms` sin cambio de formato; seeder idempotente converge en UQ. |

## 2. Escenarios Gherkin (Pass 2 — formales; Pass 3 — adversarial OK)

- **SEC-G1 Editar matriz rol funcional:** `Given` soy `System Manager` `And` rol `Sales Manager` existe `When` `PUT /api/v1/roles/{id}/permissions` con `[{doctype: sales_order, read: true, write: true}]` `Then` `200` y `GET` refleja la matriz. `And` `UpdatePermissions` sobre `System Manager` → `403 SYSTEM_MANAGER_IMMUTABLE` (no tiene rows; nada que editar).
- **SEC-G2 Crear rol:** `Given` soy `System Manager` `When` `POST /api/v1/roles {name: "Support L1", permissions: [...]}` `Then` `201` con `IsSystemDefault: false`. `And` nombre duplicado en el tenant → `409 ROLE_NAME_CONFLICT`. `And` nombre duplicado en **otro** tenant → `201` (UQ incluye `TenantId`).
- **SEC-G3 Asignar/remover roles:** `Given` soy `System Manager` `When` `POST /api/v1/users/{id}/roles {roleId}` `Then` `204` y próximo login del usuario trae el claim `roles` actualizado. `When` `DELETE /api/v1/users/{id}/roles/{roleId}` `Then` `204`. `And` asignar rol de otro tenant → `403 CROSS_TENANT_ASSIGNMENT_DENIED`. `And` doble asignación → `409 USER_ROLE_ALREADY_ASSIGNED`.
- **SEC-G4 403 sin permiso:** `Given` usuario solo con rol `HR Manager` `When` `GET /api/v1/purchase-orders` (policy `permission:purchase_order:read`) `Then` `403` `ProblemDetails` sin filtrar `TenantId` ajeno.
- **SEC-G5 Protección System Manager + último admin:** `When` `DELETE /api/v1/roles/{systemManagerId}` o `PUT` matriz sobre él → `403 SYSTEM_MANAGER_IMMUTABLE`. `When` remover rol `System Manager` del único usuario que lo posee en el tenant → `409 CANNOT_REMOVE_LAST_ADMIN`. `When` removerlo de un admin habiendo ≥2 → `204`.
- **SEC-G6 Token / próximo login:** `Given` cambio de roles/permisos confirmado `When` el usuario afectado llama con su JWT vigente `Then` conserva claims viejos hasta re-login. `And` la UI muestra aviso "los cambios aplican en el próximo inicio de sesión". **Sin** endpoint de revocación server-side (fuera de alcance: `JwtTokenGenerator` no tiene refresh/revocación).

## 3. DDL (Pass 2 — sin migración destructiva)

- `Roles`: `Id UNIQUEIDENTIFIER PK, TenantId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(100) NOT NULL, Description NVARCHAR(500) NULL, IsSystemDefault BIT NOT NULL DEFAULT 0`. `UQ_Roles_Tenant_Name UNIQUE (TenantId, Name)`. `IX_Roles_Tenant (TenantId)`. Query filter `ITenantEntity` (Art II); `TenantId` inmutable en `SaveChangesAsync`.
- `DocTypePermissions`: `Id, TenantId, RoleId FK→Roles(Id, CASCADE), DocType NVARCHAR(100), CanRead/Write/Create/Delete/Submit/Cancel BIT`. `UQ_DocPerms_Tenant_Role_DocType UNIQUE (TenantId, RoleId, DocType)`. `IX_DocPerms_Tenant_Role_Doc (TenantId, RoleId, DocType)` (TenantId líder, Art IV).
- `UserRoles`: `Id, TenantId, UserId FK→Users, RoleId FK→Roles(CASCADE)`. `UQ_UserRoles_Tenant_User_Role UNIQUE (TenantId, UserId, RoleId)`. `IX_UserRoles_Tenant_User (TenantId, UserId)`, `IX_UserRoles_Tenant_Role (TenantId, RoleId)`.
- Invariante adversarial (Pass 3): el seeder converge en `UQ` violation → `catch` idempotente ya existente; no cambiar Guids hardcodeados de mapeo.

## 4. CQRS + API + Errores (Pass 2; Pass 3 — códigos snake_case OK)

- `CreateRoleCommand {Name, Description, Permissions[]}` → `201`; validador: `Name` 2..100, `DocType` en catálogo §5, `Create ⇒ Write` (si `Create=true` y `Write=false` → `400` o auto-coerce documentada; elegido: **rechazar con `400`** para hacer la semántica explícita).
- `UpdateRolePermissionsCommand {RoleId, Permissions[]}` → `200`; **rechaza** `IsSystemDefault && Name=="System Manager"` con `403 SYSTEM_MANAGER_IMMUTABLE`.
- `DeleteRoleCommand {RoleId}` → `204`; rechaza sistema con `403`; rol con usuarios asignados → `409` (o `400` con conteo; elegido `409 ROLE_HAS_ASSIGNMENTS` — agregar código con mismo formato).
- `AssignRoleCommand {UserId, RoleId}` / `RemoveRoleCommand {UserId, RoleId}` → `204`; validan mismo `TenantId` (`403 CROSS_TENANT_ASSIGNMENT_DENIED`); último admin guard (`409 CANNOT_REMOVE_LAST_ADMIN`).
- `GetRolesQuery` / `GetRoleDetailQuery` → `200` (paginado; `IsSystemDefault` expuesto, matriz incluida).
- `RolesController`: `[ApiController] [Route("api/v1/[controller]")] [Authorize(Policy="TenantMember")] [Produces("application/json")] [Consumes("application/json")]` + `[ProducesResponseType]` por acción (Art VI). `UserRoles` bajo `UsersController` o `RolesController` (`/api/v1/users/{id}/roles`) — misma política.
- RFC7807: `404 ROLE_NOT_FOUND / USER_ROLE_NOT_FOUND`, `400` validación, `403 SYSTEM_MANAGER_IMMUTABLE / CROSS_TENANT_ASSIGNMENT_DENIED`, `409 ROLE_NAME_CONFLICT / CANNOT_REMOVE_LAST_ADMIN / USER_ROLE_ALREADY_ASSIGNED / ROLE_HAS_ASSIGNMENTS`. Todos con entrada resx EN/ES (coverage test) + `error.json` vía `i18n-sync.mjs`.

## 5. Catálogo DocTypes + matriz acciones (Pass 2)

Agrupados por módulo (solo DocTypes reales del seeder):

- **Sales:** `sales_order, sales_invoice, delivery_note, customer, lead, opportunity`
- **Stock/Item:** `item, warehouse, stock_entry, stock`
- **Catalog/Core:** `company, currency, catalog`
- **Purchase:** `purchase_order, purchase_invoice, purchase_receipt, supplier`
- **Accounting:** `account, journal_entry, payment_entry, gl_entry, bank_account, bank_transaction, bank_statement_import, bank_transaction_rule, fiscal_year, period_closing_voucher, exchange_rate, exchange_rate_revaluation, asset, asset_category, report`
- **HR:** `employee, attendance, leave_application, salary_component, salary_structure, payroll_entry`
- **Manufacturing/Projects:** `bom, work_order, project`

Acciones: `Read | Write | Create | Delete | Submit | Cancel`. Semántica: `Create ⇒ Write` (crear exige escribir; validar en comando); `Delete=false` por defecto salvo `System Manager` (bypass sin rows); `Submit/Cancel` solo DocTypes transaccionales (órdenes, facturas, asientos, work orders); `Read` independiente.

## 6. UI (Art VII; Pass 3 — autorización server-side)

- `src/features/security/`: `rolesApi.ts` (vía `src/api/client.ts`, `X-Tenant-ID` automático), `RolesPage`, `RoleEditor` (matriz DocType×acción con `Create⇒Write` reactivo), `AssignRolesDialog`, store Zustand.
- Menú/entrada visible solo si `roles` incluye `System Manager` (UX), **pero la autorización real queda server-side** (`TenantMember` + policies `permission:` + guard sistema). Nunca confiar en el gate del menú.
- Errores vía `ProblemDetails` + `error.json` i18n; aviso "aplica en próximo login" tras mutaciones.

## 7. Adversarial checklist (Pass 3 — certificado)

- [x] Códigos en `UPPER_SNAKE`, no `VARCHAR`/frases; cada uno en `ErrorMessages.resx` + `ErrorMessages.es.resx` (coverage test verde) + `error.json` sincronizado.
- [x] `UpdatePermissions`/`Delete` rechazan rol sistema (`403 SYSTEM_MANAGER_IMMUTABLE`); matriz de System Manager inexistente por diseño (bypass), no "matriz vacía editable".
- [x] `Assign/Remove` validan mismo tenant (`403` cross-tenant); guard último admin (`409`) evaluado por tenant.
- [x] `TenantId` nunca mutable desde API; sin `.Where(TenantId)` manual (query filter automático).
- [x] Sin `IsSystemRole` inventado; sin refresh-token inventado; sin endpoints fuera de `api/v1/[controller]` + `TenantMember`.
