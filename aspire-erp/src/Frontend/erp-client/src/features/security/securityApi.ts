/**
 * Roles & permissions wire client (triad: Role & Permission Manager, spec §4).
 *
 * Backend contract (programmed against, not waited for — Fase 1 runs in parallel):
 *   GET  /api/v1/roles                     paged list of roles
 *   POST /api/v1/roles                     201, { name, description?, permissions? }
 *   GET  /api/v1/roles/{id}/permissions    200, permission rows for the role
 *   PUT  /api/v1/roles/{id}/permissions    200, full-matrix rewrite
 *   PUT  /api/v1/users/{id}/roles          204, { roleIds } (full assignment set)
 *   DELETE /api/v1/users/{id}/roles/{roleId} 204, single removal
 *
 * All calls go through the centralized `apiClient` (X-Tenant-ID + Bearer + RFC7807
 * `ApiError` normalization). Every list reader tolerates an empty backend / 404 and
 * degrades to `[]` so the UI renders before Fase 1 lands; mutating calls rethrow so
 * the view can surface codes like `CANNOT_REMOVE_LAST_ADMIN` (409) semantically.
 */
import { ApiError, apiClient } from '../../api/client'
import { DOC_TYPES_CATALOG, type DocTypeInfo } from './docTypesCatalog'

export interface RoleDto {
  id: string
  name: string
  description?: string | null
  isSystemDefault: boolean
  userCount?: number
}

export interface DocTypePermissionDto {
  docType: string
  canRead: boolean
  canWrite: boolean
  canCreate: boolean
  canDelete: boolean
  canSubmit: boolean
  canCancel: boolean
}

export interface UserWithRolesDto {
  id: string
  email: string
  fullName: string
  roleIds: string[]
  roleNames: string[]
}

export interface CreateRoleRequest {
  name: string
  description?: string
  permissions?: DocTypePermissionDto[]
}

interface PagedEnvelope<T> {
  items?: T[]
}

const asArray = <T>(payload: unknown): T[] => {
  if (Array.isArray(payload)) return payload as T[]
  if (payload && typeof payload === 'object') {
    const items = (payload as PagedEnvelope<T>).items
    if (Array.isArray(items)) return items
  }
  return []
}

const toBool = (value: unknown): boolean => value === true

const toString = (value: unknown): string =>
  typeof value === 'string' ? value : value == null ? '' : String(value)

function normalizeRole(raw: unknown): RoleDto | null {
  if (!raw || typeof raw !== 'object') return null
  const r = raw as Record<string, unknown>
  const id = toString(r.id ?? r.roleId)
  const name = toString(r.name)
  if (!id || !name) return null
  return {
    id,
    name,
    description: typeof r.description === 'string' ? r.description : null,
    isSystemDefault: toBool(r.isSystemDefault ?? r.isSystem ?? r.systemDefault),
    userCount:
      typeof r.userCount === 'number'
        ? r.userCount
        : typeof r.usersCount === 'number'
          ? (r.usersCount as number)
          : undefined,
  }
}

/**
 * Accepts both the canonical camelCase shape and the SEC-G1 shorthand
 * (`{ doctype, read, write, create, delete, submit, cancel }`), so the UI keeps
 * working regardless of which casing Fase 1 serializes.
 */
export function normalizePermissionRow(raw: unknown): DocTypePermissionDto | null {
  if (!raw || typeof raw !== 'object') return null
  const r = raw as Record<string, unknown>
  const docType = toString(r.docType ?? r.doctype ?? r.doc_type)
  if (!docType) return null
  return {
    docType,
    canRead: toBool(r.canRead ?? r.read),
    canWrite: toBool(r.canWrite ?? r.write),
    canCreate: toBool(r.canCreate ?? r.create),
    canDelete: toBool(r.canDelete ?? r.delete),
    canSubmit: toBool(r.canSubmit ?? r.submit),
    canCancel: toBool(r.canCancel ?? r.cancel),
  }
}

function extractStringIds(value: unknown): string[] {
  if (!Array.isArray(value)) return []
  const out: string[] = []
  for (const entry of value) {
    if (typeof entry === 'string' && entry !== '') out.push(entry)
    else if (entry && typeof entry === 'object') {
      const id = toString((entry as Record<string, unknown>).id ?? (entry as Record<string, unknown>).roleId)
      if (id) out.push(id)
    }
  }
  return out
}

function normalizeUser(raw: unknown): UserWithRolesDto | null {
  if (!raw || typeof raw !== 'object') return null
  const r = raw as Record<string, unknown>
  const id = toString(r.id ?? r.userId)
  if (!id) return null
  const email = toString(r.email)
  const fullName = toString(r.fullName ?? r.name ?? r.displayName ?? email)
  const roleIds = extractStringIds(r.roleIds ?? r.roles ?? r.roleIdList)
  const roleNames = Array.isArray(r.roleNames)
    ? (r.roleNames as unknown[]).filter((n): n is string => typeof n === 'string')
    : []
  return { id, email, fullName, roleIds, roleNames }
}

export async function getRolesApi(): Promise<RoleDto[]> {
  const { data } = await apiClient.get<unknown>('/v1/roles')
  return asArray<unknown>(data)
    .map(normalizeRole)
    .filter((r): r is RoleDto => r !== null)
}

export async function createRoleApi(request: CreateRoleRequest): Promise<RoleDto> {
  const { data } = await apiClient.post<unknown>('/v1/roles', {
    name: request.name,
    description: request.description ?? null,
    permissions: request.permissions ?? [],
  })
  const created = normalizeRole(data)
  if (!created) throw new ApiError(201, 'Unexpected Response', 'The server returned an unparsable role.')
  return created
}

export async function getRolePermissionsApi(roleId: string): Promise<DocTypePermissionDto[]> {
  const { data } = await apiClient.get<unknown>(`/v1/roles/${roleId}/permissions`)
  return asArray<unknown>(data)
    .map(normalizePermissionRow)
    .filter((p): p is DocTypePermissionDto => p !== null)
}

export async function updateRolePermissionsApi(
  roleId: string,
  permissions: DocTypePermissionDto[],
): Promise<void> {
  await apiClient.put(`/v1/roles/${roleId}/permissions`, permissions)
}

/**
 * No triad endpoint lists users with their roles (the users catalog belongs to another
 * module). Tries the conventional `/v1/users` collection and tolerates a 404/empty
 * backend with `[]`; any other failure rethrows for the view to report.
 */
export async function getUsersWithRolesApi(): Promise<UserWithRolesDto[]> {
  try {
    const { data } = await apiClient.get<unknown>('/v1/users', {
      params: { page: 1, pageSize: 500 },
    })
    return asArray<unknown>(data)
      .map(normalizeUser)
      .filter((u): u is UserWithRolesDto => u !== null)
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 404) return []
    throw cause
  }
}

/** Full-set assignment (triad contract). 409 CANNOT_REMOVE_LAST_ADMIN surfaces as ApiError. */
export async function assignUserRolesApi(userId: string, roleIds: string[]): Promise<void> {
  await apiClient.put(`/v1/users/${userId}/roles`, { roleIds })
}

export async function removeUserRoleApi(userId: string, roleId: string): Promise<void> {
  await apiClient.delete(`/v1/users/${userId}/roles/${roleId}`)
}

/**
 * The triad defines no DocType catalog endpoint, so this resolves the local mirror
 * of spec §5 (`docTypesCatalog.ts`). Kept async so callers can transparently prefer
 * a future wire catalog without changing shape.
 */
export async function getDocTypesCatalogApi(): Promise<readonly DocTypeInfo[]> {
  return DOC_TYPES_CATALOG
}
