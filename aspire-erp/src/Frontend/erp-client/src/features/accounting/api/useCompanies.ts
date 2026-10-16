import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'

/**
 * Mirrors `CompanyDto` (GET /api/v1/companies) as serialized by System.Text.Json: camelCase
 * properties. Only the fields the supplier form needs are modelled.
 */
export interface Company {
  id: string
  name: string
  frozenAccountsDate?: string | null
  defaultRetainedEarningsAccountId?: string | null
  defaultRetainedEarningsAccountCode?: string | null
}

export type CompanyStatus = 'idle' | 'loading' | 'success' | 'error'

/**
 * Loads the tenant's companies (ordered by name) so a per-company Supplier account mapping can
 * be edited for each company (module buying-suppliers, spec scenario SUP-G1). Small, shared
 * catalog: loaded once per mount like `useCurrencies`.
 */
export function useCompanies() {
  const [items, setItems] = useState<Company[]>([])
  const [status, setStatus] = useState<CompanyStatus>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  const fetchCompanies = useCallback(async () => {
    setStatus('loading')
    try {
      const response = await apiClient.get<Company[]>('/v1/companies')
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
    fetchCompanies()
  }, [fetchCompanies])

  return { items, status, error, reload: fetchCompanies }
}
