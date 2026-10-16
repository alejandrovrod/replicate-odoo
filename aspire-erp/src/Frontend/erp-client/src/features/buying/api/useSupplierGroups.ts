import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'

/**
 * Mirrors `SupplierGroupDto` (GET/POST /api/v1/supplier-groups) as serialized by
 * System.Text.Json: camelCase properties.
 */
export interface SupplierGroup {
  id: string
  code: string
  name: string
  isActive: boolean
  createdAt: string
}

export type SupplierGroupStatus = 'idle' | 'loading' | 'success' | 'error'

/**
 * Loads the tenant's supplier group catalog (module buying-suppliers, task T4.1). The catalog
 * is small and shared by the supplier list filter and the supplier form, so it loads once per
 * mount like `useCurrencies`.
 */
export function useSupplierGroups() {
  const [items, setItems] = useState<SupplierGroup[]>([])
  const [status, setStatus] = useState<SupplierGroupStatus>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  const fetchGroups = useCallback(async () => {
    setStatus('loading')
    try {
      const response = await apiClient.get<SupplierGroup[]>('/v1/supplier-groups')
      setItems(response.data ?? [])
      setStatus('success')
    } catch (cause: unknown) {
      setError(
        cause instanceof ApiError
          ? cause
          : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause)),
      )
      setStatus('error')
    }
  }, [])

  useEffect(() => {
    fetchGroups()
  }, [fetchGroups])

  return { items, status, error, reload: fetchGroups }
}

/** POST body (mirrors `CreateSupplierGroupCommand`). */
export async function createSupplierGroup(payload: {
  code: string
  name: string
  isActive?: boolean
}): Promise<SupplierGroup> {
  const response = await apiClient.post<SupplierGroup>('/v1/supplier-groups', {
    code: payload.code,
    name: payload.name,
    isActive: payload.isActive ?? true,
  })
  return response.data
}
