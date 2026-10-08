import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'

/**
 * Mirrors `CurrencyDto` (GET /api/v1/currencies, RM-09 global ISO catalog) as serialized
 * by System.Text.Json: camelCase properties, `rowVersion` as a base64 string.
 */
export interface Currency {
  id: string
  code: string
  symbol: string
  fractionName: string
  isActive: boolean
  createdAt: string
  /** Base64 rowversion token from GET; echoed back on PUT for optimistic concurrency. */
  rowVersion?: string
}

export type CurrencyStatus = 'idle' | 'loading' | 'success' | 'error'

/**
 * Loads the global currency catalog once (tiny, shared, cacheable by the caller).
 * `onlyActive` mirrors the backend default (active currencies for dropdowns).
 */
export function useCurrencies(onlyActive = true) {
  const [items, setItems] = useState<Currency[]>([])
  const [status, setStatus] = useState<CurrencyStatus>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  const fetchCurrencies = useCallback(async () => {
    setStatus('loading')
    try {
      const response = await apiClient.get<Currency[]>('/v1/currencies', {
        params: { onlyActive },
      })
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
  }, [onlyActive])

  useEffect(() => {
    fetchCurrencies()
  }, [fetchCurrencies])

  return { items, status, error, reload: fetchCurrencies }
}

/** POST body (mirrors `CreateCurrencyCommand`). */
export async function createCurrency(payload: {
  code: string
  symbol: string
  fractionName?: string
  isActive?: boolean
}): Promise<Currency> {
  const response = await apiClient.post<Currency>('/v1/currencies', payload)
  return response.data
}

/**
 * PUT body (mirrors `UpdateCurrencyCommand`): always carries the original
 * `rowVersion` so a concurrent change resolves to 409 instead of silently winning.
 */
export async function updateCurrency(
  id: string,
  payload: { code: string; symbol: string; fractionName?: string; isActive: boolean; rowVersion?: string },
): Promise<Currency> {
  const response = await apiClient.put<Currency>(`/v1/currencies/${id}`, { id, ...payload })
  return response.data
}
