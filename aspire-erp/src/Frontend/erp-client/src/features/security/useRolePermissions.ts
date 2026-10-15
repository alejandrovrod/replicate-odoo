/**
 * Reactive permission-matrix state for the selected role.
 *
 * - Loads roles once, then lazily loads each role's matrix and caches it, so
 *   switching back to a previously opened role is instant (reactive cache).
 * - `Create ⇒ Write` is enforced reactively: granting Create forces Write on;
 *   revoking Write also revokes Create (spec §5).
 * - RFC7807 failures surface as `ApiError` state; the view renders them with
 *   `role="alert"` (and `translateErrorCode` for known codes).
 */
import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { DOC_TYPES_CATALOG, PERMISSION_ACTIONS, type PermissionAction } from './docTypesCatalog'
import {
  getRolePermissionsApi,
  getRolesApi,
  updateRolePermissionsApi,
  type DocTypePermissionDto,
  type RoleDto,
} from './securityApi'

export type { PermissionAction }
export type MatrixState = Record<string, Record<PermissionAction, boolean>>

export const emptyRow = (): Record<PermissionAction, boolean> => ({
  read: false,
  write: false,
  create: false,
  delete: false,
  submit: false,
  cancel: false,
})

export function buildMatrix(rows: DocTypePermissionDto[]): MatrixState {
  const matrix: MatrixState = {}
  for (const info of DOC_TYPES_CATALOG) matrix[info.docType] = emptyRow()
  for (const row of rows) {
    matrix[row.docType] = {
      read: row.canRead,
      write: row.canWrite,
      create: row.canCreate,
      delete: row.canDelete,
      submit: row.canSubmit,
      cancel: row.canCancel,
    }
  }
  return matrix
}

export function matrixToDtos(matrix: MatrixState): DocTypePermissionDto[] {
  return Object.entries(matrix).map(([docType, flags]) => ({
    docType,
    canRead: flags.read,
    canWrite: flags.write,
    canCreate: flags.create,
    canDelete: flags.delete,
    canSubmit: flags.submit,
    canCancel: flags.cancel,
  }))
}

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

export function useRolePermissions() {
  const [roles, setRoles] = useState<RoleDto[]>([])
  const [rolesLoading, setRolesLoading] = useState(true)
  const [rolesError, setRolesError] = useState<ApiError | null>(null)
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null)

  const [matrix, setMatrix] = useState<MatrixState>(() => buildMatrix([]))
  const [matrixLoading, setMatrixLoading] = useState(false)
  const [matrixError, setMatrixError] = useState<ApiError | null>(null)
  const [dirty, setDirty] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<ApiError | null>(null)
  const [saved, setSaved] = useState(false)

  const cacheRef = useRef<Map<string, MatrixState>>(new Map())

  const loadRoles = useCallback(async () => {
    setRolesLoading(true)
    setRolesError(null)
    try {
      const list = await getRolesApi()
      setRoles(list)
      setSelectedRoleId((current) => {
        if (current && list.some((r) => r.id === current)) return current
        return list[0]?.id ?? null
      })
    } catch (cause) {
      setRolesError(toApiError(cause))
      setRoles([])
    } finally {
      setRolesLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadRoles()
  }, [loadRoles])

  const selectedRole: RoleDto | null = roles.find((r) => r.id === selectedRoleId) ?? null
  const isSystemLocked = selectedRole?.name === 'System Manager'

  useEffect(() => {
    if (!selectedRoleId) return
    const cached = cacheRef.current.get(selectedRoleId)
    if (cached) {
      setMatrix(cached)
      setMatrixError(null)
      setDirty(false)
      setSaved(false)
      return
    }
    let cancelled = false
    setMatrixLoading(true)
    setMatrixError(null)
    setDirty(false)
    setSaved(false)
    getRolePermissionsApi(selectedRoleId).then(
      (rows) => {
        if (cancelled) return
        const next = buildMatrix(rows)
        cacheRef.current.set(selectedRoleId, next)
        setMatrix(next)
        setMatrixLoading(false)
      },
      (cause: unknown) => {
        if (cancelled) return
        // Tolerate an empty backend (Fase 1 in parallel): start from a blank matrix
        // but still report the failure so the user knows nothing was loaded.
        setMatrix(buildMatrix([]))
        setMatrixError(toApiError(cause))
        setMatrixLoading(false)
      },
    )
    return () => {
      cancelled = true
    }
  }, [selectedRoleId])

  const mutateRow = useCallback(
    (docType: string, mutate: (row: Record<PermissionAction, boolean>) => void) => {
      if (isSystemLocked) return
      setMatrix((prev) => {
        const next: MatrixState = { ...prev, [docType]: { ...prev[docType] } }
        mutate(next[docType])
        return next
      })
      setDirty(true)
      setSaved(false)
      setSaveError(null)
    },
    [isSystemLocked],
  )

  const toggleCell = useCallback(
    (docType: string, action: PermissionAction) => {
      mutateRow(docType, (row) => {
        const next = !row[action]
        row[action] = next
        // Create ⇒ Write (spec §5): granting Create forces Write; revoking
        // Write revokes Create so the pair can never contradict the validator.
        if (action === 'create' && next) row.write = true
        if (action === 'write' && !next) row.create = false
      })
    },
    [mutateRow],
  )

  const toggleRow = useCallback(
    (docType: string) => {
      mutateRow(docType, (row) => {
        const allOn = PERMISSION_ACTIONS.every((a) => row[a])
        for (const a of PERMISSION_ACTIONS) row[a] = !allOn
      })
    },
    [mutateRow],
  )

  const toggleColumn = useCallback(
    (action: PermissionAction) => {
      if (isSystemLocked) return
      setMatrix((prev) => {
        const allOn = Object.values(prev).every((row) => row[action])
        const next: MatrixState = {}
        for (const [docType, row] of Object.entries(prev)) {
          const copy = { ...row, [action]: !allOn }
          // Keep the Create ⇒ Write invariant under column toggles too.
          if (copy.create) copy.write = true
          if (action === 'write' && allOn) copy.create = false
          next[docType] = copy
        }
        return next
      })
      setDirty(true)
      setSaved(false)
      setSaveError(null)
    },
    [isSystemLocked],
  )

  const save = useCallback(async () => {
    if (!selectedRoleId || isSystemLocked || !dirty) return
    setSaving(true)
    setSaveError(null)
    try {
      const dtos = matrixToDtos(matrix)
      await updateRolePermissionsApi(selectedRoleId, dtos)
      cacheRef.current.set(selectedRoleId, matrix)
      setDirty(false)
      setSaved(true)
    } catch (cause) {
      setSaveError(toApiError(cause))
    } finally {
      setSaving(false)
    }
  }, [selectedRoleId, isSystemLocked, dirty, matrix])

  const selectRole = useCallback((roleId: string) => {
    setSelectedRoleId(roleId)
    setSaveError(null)
  }, [])

  return {
    roles,
    rolesLoading,
    rolesError,
    reloadRoles: loadRoles,
    selectedRoleId,
    selectRole,
    selectedRole,
    isSystemLocked,
    matrix,
    matrixLoading,
    matrixError,
    dirty,
    saving,
    saveError,
    saved,
    toggleCell,
    toggleRow,
    toggleColumn,
    save,
  }
}
