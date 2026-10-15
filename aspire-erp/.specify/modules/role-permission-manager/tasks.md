> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Tasks: Role & Permission Manager

## Dominio → App → Api → UI (orden obligatorio, D5)

- [ ] `Infra`: migración aditiva UQ + índices §3 spec (`TenantId` líder); verificar query filters `ITenantEntity` y `TenantId` inmutable en `SaveChangesAsync`.
- [ ] `Domain`: invariante `IsSystemManager` (derivado de `IsSystemDefault && Name`); **no** crear `IsSystemRole`.
- [ ] `App`: `CreateRoleCommand/Handler/Validator` (`Create⇒Write` → `400`, `409 ROLE_NAME_CONFLICT`).
- [ ] `App`: `UpdateRolePermissionsCommand/Handler/Validator` (`403 SYSTEM_MANAGER_IMMUTABLE` si sistema).
- [ ] `App`: `DeleteRoleCommand/Handler` (`403` sistema, `409 ROLE_HAS_ASSIGNMENTS`).
- [ ] `App`: `AssignRoleCommand / RemoveRoleCommand` (mismo-tenant `403 CROSS_TENANT_ASSIGNMENT_DENIED`, último admin `409 CANNOT_REMOVE_LAST_ADMIN`, `409 USER_ROLE_ALREADY_ASSIGNED` / `404 USER_ROLE_NOT_FOUND`).
- [ ] `App`: `GetRolesQuery` (paginado) + `GetRoleDetailQuery` (con matriz).
- [ ] `Shared`: 8 códigos `AuthErrorCodes` `UPPER_SNAKE` + resx EN/ES + `error.json` vía `i18n-sync.mjs`; `ErrorCatalogCoverageTests` verde.
- [ ] `Api`: `RolesController` (Art VI: `ApiController`, `Route api/v1/[controller]`, `TenantMember`, `Produces/Consumes`, `ProducesResponseType`) + rutas users-roles; RFC7807 400/403/404/409.
- [ ] `Api tests`: SEC-G1 (editar matriz + rechazo sistema), SEC-G2 (crear + 409), SEC-G3 (assign/remove + cross-tenant 403), SEC-G4 (403 sin permiso), SEC-G5 (sistema + último admin), SEC-G6 (claims en próximo login).
- [ ] `UI`: `src/features/security/rolesApi.ts` (axios centralizado) + store Zustand.
- [ ] `UI`: `RolesPage` + `RoleEditor` (matriz por módulo, `Create⇒Write` reactivo, sistema read-only) + `AssignRolesDialog` + aviso próximo-login + i18n errores.
- [ ] `UI`: gate menú `System Manager` solo UX; autorización server-side verificada (SEC-G4).
- [ ] `Compat`: re-seed idempotente OK, bypass handler intacto, JWT `roles/perms` sin cambio formato, expiración 24h documentada (sin refresh inventado).
