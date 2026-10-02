import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../api/client'
import type { Item, StockEntry, StockEntryPosting, StockEntryType, WarehouseNode } from './types'

export type QueryStatus = 'idle' | 'loading' | 'success' | 'error'

interface QueryState<T> {
  key: string
  attempt: number
  status: 'success' | 'error'
  data: T
  error: ApiError | null
}

/**
 * Stable empty array so the effect below never re-runs because a caller rebuilt its literal.
 * Every stock query returns a list, so one placeholder serves them all.
 */
const EMPTY: never[] = []

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

/**
 * Shared GET loader for the stock feature (same contract as `useAccountTree`): tenant header
 * injection and RFC 7807 handling live in `src/api/client.ts`.
 *
 * `params` is an object literal recreated on every render, so the effect keys off its JSON
 * round-trip (`paramsKey`) instead of its identity - keying on identity would refetch forever,
 * and keying on nothing would silently serve a stale company. The parse inside the effect is
 * what lets `params` stay out of the dependency list.
 */
function useApiList<T>(path: string, params: Record<string, string | number>, enabled: boolean) {
  const paramsKey = JSON.stringify(params)
  const key = `${path}?${paramsKey}`
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<QueryState<T[]> | null>(null)

  useEffect(() => {
    if (!enabled) return undefined

    let cancelled = false
    const query = JSON.parse(paramsKey) as Record<string, string | number>
    apiClient.get<T[]>(path, { params: query }).then(
      (response) => {
        if (!cancelled) setState({ key, attempt, status: 'success', data: response.data, error: null })
      },
      (cause: unknown) => {
        if (!cancelled) setState({ key, attempt, status: 'error', data: EMPTY as T[], error: toApiError(cause) })
      },
    )

    return () => {
      cancelled = true
    }
  }, [path, paramsKey, attempt, enabled, key])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  if (!enabled) return { data: EMPTY as T[], status: 'idle' as const, error: null, reload }
  if (state && state.key === key && state.attempt === attempt) {
    return { data: state.data, status: state.status, error: state.error, reload }
  }
  return { data: EMPTY as T[], status: 'loading' as const, error: null, reload }
}

/** GET /api/v1/items?companyId= - SKUs with their live per-warehouse stock (Task 3.4). */
export function useItems(companyId: string) {
  return useApiList<Item>('/v1/items', { companyId }, Boolean(companyId))
}

/** GET /api/v1/warehouses/tree?companyId= - the nested warehouse hierarchy. */
export function useWarehouses(companyId: string) {
  return useApiList<WarehouseNode>('/v1/warehouses/tree', { companyId }, Boolean(companyId))
}

/** GET /api/v1/stockentries?companyId=&limit= - the most recent posted vouchers. */
export function useStockEntries(companyId: string, limit = 20) {
  return useApiList<StockEntry>('/v1/stockentries', { companyId, limit }, Boolean(companyId))
}

/** Body of POST /api/v1/stockentries (mirrors `CreateStockEntryCommand`). */
export interface CreateStockEntryPayload {
  companyId: string
  entryType: StockEntryType
  warehouseId: string
  targetWarehouseId?: string
  postingDate: string
  lines: { itemId: string; qty: number; rate?: number }[]
}

/**
 * POST /api/v1/stockentries - creates AND posts the voucher atomically.
 *
 * `Idempotency-Key` is mandatory (Constitution VI.4): replaying the same key with the same body
 * returns the original 201 instead of posting twice, which is what makes a retry of the same
 * modal submission safe. The server releases the reservation when the request fails, so the
 * form can be corrected and re-posted under the same key.
 */
export async function postStockEntry(
  payload: CreateStockEntryPayload,
  idempotencyKey: string,
): Promise<StockEntryPosting> {
  const response = await apiClient.post<StockEntryPosting>('/v1/stockentries', payload, {
    headers: { 'Idempotency-Key': idempotencyKey },
  })
  return response.data
}
