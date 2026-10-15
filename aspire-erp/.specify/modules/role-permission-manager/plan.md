> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Plan: Role & Permission Manager

## Phase 0 — Precondiciones (sin código)

1. Congelar catálogo DocTypes §5 del `spec.md` (solo strings reales del seeder).
2. Reservar códigos `ROLE_NOT_FOUND, ROLE_NAME_CONFLICT, ROLE_HAS_ASSIGNMENTS, SYSTEM_MANAGER_IMMUTABLE, CANNOT_REMOVE_LAST_ADMIN, USER_ROLE_ALREADY_ASSIGNED, USER_ROLE_NOT_FOUND, CROSS_TENANT_ASSIGNMENT_DENIED` en `AuthErrorCodes.cs` + resx EN/ES + `error.json` (`scripts/i18n-sync.mjs`); correr `ErrorCatalogCoverageTests`.

## Phase 1 — Dominio (Art I: cero deps)

1. Extender configuración EF (Infrastructure, no Dominio): UQ `Roles(TenantId,Name)`, `DocTypePermissions(TenantId,RoleId,DocType)`, `UserRoles(TenantId,UserId,RoleId)` + índices con `TenantId` líder (Art IV). Migración solo aditiva.
2. Invariante dominio: `Role` expone `IsSystemManager => IsSystemDefault && Name == "System Manager"` (sin agregar columna `IsSystemRole`); `TenantId` init-only/inmutable.

## Phase 2 — Application CQRS + validación

1. `CreateRoleCommand + Validator` (Name 2..100, DocType en catálogo, `Create⇒Write` → `400`), `UpdateRolePermissionsCommand + Validator` (rechazo sistema `403`), `DeleteRoleCommand` (rechazo sistema `403`, con asignaciones `409 ROLE_HAS_ASSIGNMENTS`), `AssignRoleCommand / RemoveRoleCommand` (mismo-tenant `403`, último-admin `409`).
2. Queries `GetRoles (paginado)` / `GetRoleDetail (con matriz)`. Handlers sin `.Where(TenantId)` manual (query filter Art II); `SaveChangesAsync` impone `TenantId`.
3. Tests: SEC-G1..G6 (incl. `UpdatePermissions` sistema → `403`, cross-tenant → `403`, último admin → `409`, token aplica en próximo login).

## Phase 3 — API (Art VI)

1. Nuevo `RolesController`: `[ApiController] [Route("api/v1/[controller]")] [Authorize(Policy="TenantMember")] [Produces/Consumes("application/json")]` + `[ProducesResponseType]` por acción (201/200/204/400/403/404/409 + `ProblemDetails`).
2. Rutas: `POST /api/v1/roles`, `GET /api/v1/roles`, `GET /api/v1/roles/{id}`, `PUT /api/v1/roles/{id}/permissions`, `DELETE /api/v1/roles/{id}`, `POST|DELETE /api/v1/users/{id}/roles[/{roleId}]`.
3. Sin `[IdempotencyKeyRequired]` (no hay posteo contable). Verificar que policies `permission:` + bypass `IsInRole("System Manager")` siguen intactos.

## Phase 4 — Frontend (Art VII)

1. `src/features/security/rolesApi.ts` vía `src/api/client.ts` (axios + `X-Tenant-ID` + `ProblemDetails`); store Zustand.
2. `RolesPage` (lista + badge `System`), `RoleEditor` (matriz agrupada por módulo, `Create⇒Write` reactivo, rol sistema read-only con aviso), `AssignRolesDialog` + aviso "aplica en próximo login".
3. Gate de menú por `roles includes "System Manager"` solo UX; autorización real server-side.

## Phase 5 — Compat seeder/JWT (D6)

1. `SecurityRoleSeeder` sin cambios de comportamiento: idempotencia por UQ converge; System Manager sigue sin rows.
2. `JwtTokenGenerator` sin cambios de formato (`roles` + `perms`); documentar expiración 24h + login-forzado. **No** agregar refresh/revocación.
3. Re-seed en tenant de prueba + login → claims verificados.

## Riesgos

- Inventar `IsSystemRole`/refresh-tokens → mitigado (prohibido en spec §7). Saltarse resx/`error.json` → bloquea coverage test. Gate solo-cliente → mitigado con policies server-side.
