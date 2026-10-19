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

/** User Management read model (`GET /api/v1/users`), spec D2/D4. */
export interface UserAdminDto extends UserWithRolesDto {
  isActive: boolean
  createdAt: string
  /** Base64 rowversion token; round-tripped on PUT for optimistic concurrency. */
  rowVersion: string | null
}

export interface PagedUsers {
  items: UserAdminDto[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
}

export interface CreateUserInput {
  fullName: string
  email: string
  temporaryPassword?: string | null
  roleIds: string[]
  isActive: boolean
}

export interface CreatedUser {
  user: UserAdminDto
  /** Password to hand over; generated server-side when the admin left the field empty. */
  temporaryPassword: string
}

export interface UpdateUserInput {
  fullName: string
  roleIds: string[]
  /** Null preserves the current lifecycle flag (edit dialog that does not manage status). */
  isActive: boolean | null
  rowVersion: string | null
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

function normalizeUserAdmin(raw: unknown): UserAdminDto | null {
  const base = normalizeUser(raw)
  if (!base) return null
  const r = raw as Record<string, unknown>
  return {
    ...base,
    isActive: r.isActive !== false,
    createdAt: toString(r.createdAt),
    rowVersion: typeof r.rowVersion === 'string' ? r.rowVersion : null,
  }
}

function normalizePage(raw: unknown): PagedUsers {
  const source = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {}
  const items = asArray<unknown>(source.items ?? raw)
    .map(normalizeUserAdmin)
    .filter((u): u is UserAdminDto => u !== null)
  const totalCount = typeof source.totalCount === 'number' ? source.totalCount : items.length
  const pageNumber = typeof source.pageNumber === 'number' ? source.pageNumber : 1
  const pageSize = typeof source.pageSize === 'number' ? source.pageSize : Math.max(1, items.length)
  const totalPages =
    typeof source.totalPages === 'number'
      ? source.totalPages
      : Math.ceil(totalCount / Math.max(1, pageSize))

  return { items, totalCount, pageNumber, pageSize, totalPages }
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
 * One page of the tenant's users (`GET /api/v1/users`), with server-side search over email and
 * full name. The backend envelope is `PagedResult<UserAdminDto>`.
 */
export async function getUsersPageApi(params: {
  page?: number
  pageSize?: number
  search?: string
} = {}): Promise<PagedUsers> {
  const { data } = await apiClient.get<unknown>('/v1/users', {
    params: {
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
      search: params.search?.trim() || undefined,
    },
  })
  return normalizePage(data)
}

export async function getUserApi(userId: string): Promise<UserAdminDto> {
  const { data } = await apiClient.get<unknown>(`/v1/users/${userId}`)
  const user = normalizeUserAdmin(data)
  if (!user) throw new ApiError(200, 'Unexpected Response', 'The server returned an unparsable user.')
  return user
}

/** 201 `{ user, temporaryPassword }`; the plaintext is shown once to the administrator. */
export async function createUserApi(input: CreateUserInput): Promise<CreatedUser> {
  const { data } = await apiClient.post<unknown>('/v1/users', {
    fullName: input.fullName,
    email: input.email,
    temporaryPassword: input.temporaryPassword?.trim() || null,
    roleIds: input.roleIds,
    isActive: input.isActive,
  })

  const source = data && typeof data === 'object' ? (data as Record<string, unknown>) : {}
  const user = normalizeUserAdmin(source.user ?? data)
  if (!user) throw new ApiError(201, 'Unexpected Response', 'The server returned an unparsable user.')

  return {
    user,
    temporaryPassword: toString(source.temporaryPassword) || (input.temporaryPassword ?? ''),
  }
}

/** 200 `UserAdminDto`; a stale token is 409 STALE_ROW_VERSION. */
export async function updateUserApi(userId: string, input: UpdateUserInput): Promise<UserAdminDto> {
  const { data } = await apiClient.put<unknown>(`/v1/users/${userId}`, {
    fullName: input.fullName,
    roleIds: input.roleIds,
    isActive: input.isActive,
    rowVersion: input.rowVersion,
  })
  const user = normalizeUserAdmin(data)
  if (!user) throw new ApiError(200, 'Unexpected Response', 'The server returned an unparsable user.')
  return user
}

/** 200 `UserAdminDto`; deactivating the last active System Manager is 409 CANNOT_REMOVE_LAST_ADMIN. */
export async function setUserActiveApi(userId: string, isActive: boolean): Promise<UserAdminDto> {
  const { data } = await apiClient.patch<unknown>(`/v1/users/${userId}/status`, { isActive })
  const user = normalizeUserAdmin(data)
  if (!user) throw new ApiError(200, 'Unexpected Response', 'The server returned an unparsable user.')
  return user
}

/** 200 `{ temporaryPassword }`: generated server-side when no password is supplied. */
export async function resetUserPasswordApi(
  userId: string,
  temporaryPassword?: string,
): Promise<string> {
  const { data } = await apiClient.post<unknown>(`/v1/users/${userId}/reset-password`, {
    temporaryPassword: temporaryPassword?.trim() || null,
  })
  const source = data && typeof data === 'object' ? (data as Record<string, unknown>) : {}
  const generated = toString(source.temporaryPassword)
  if (!generated) {
    throw new ApiError(200, 'Unexpected Response', 'The server returned no temporary password.')
  }
  return generated
}

/**
 * Compatibility reader for the roles tab: full (500-row) list flattened from the paged envelope.
 */
export async function getUsersWithRolesApi(): Promise<UserWithRolesDto[]> {
  try {
    const page = await getUsersPageApi({ page: 1, pageSize: 500 })
    return page.items
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
