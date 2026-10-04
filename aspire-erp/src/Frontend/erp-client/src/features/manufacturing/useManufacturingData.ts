import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../api/client'
import type { Bom, ManufacturingPosting, WorkOrder } from './types'

export type QueryStatus = 'idle' | 'loading' | 'success' | 'error'

interface QueryState<T> {
  key: string
  attempt: number
  status: 'success' | 'error'
  data: T
  error: ApiError | null
}

/** Stable empty array so effects never re-run because a caller rebuilt its literal. */
const EMPTY: never[] = []

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

/**
 * Shared GET loader for the manufacturing feature (same contract as the stock
 * `useApiList`): tenant header injection and RFC 7807 handling live in
 * `src/api/client.ts`. The effect keys off the JSON round-trip of `params`.
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

/** GET /api/v1/workorders?companyId= - headers for the execution board. */
export function useWorkOrders(companyId: string) {
  return useApiList<WorkOrder>('/v1/workorders', { companyId }, Boolean(companyId))
}

/** GET /api/v1/boms?companyId= - recipes for the BOM tree editor. */
export function useBoms(companyId: string) {
  return useApiList<Bom>('/v1/boms', { companyId }, Boolean(companyId))
}

/**
 * POST /api/v1/workorders/{id}/submit - Draft -> Submitted (no ledger impact, but the
 * explicit key keeps the call contract uniform with the posting mutations).
 */
export async function postSubmitWorkOrder(orderId: string, companyId: string): Promise<WorkOrder> {
  const response = await apiClient.post<WorkOrder>(
    `/v1/workorders/${orderId}/submit`,
    null,
    { params: { companyId }, headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  return response.data
}

/**
 * POST /api/v1/workorders/{id}/transfer-to-wip - Submitted -> InProcess (MF-02 voucher).
 * `Idempotency-Key` is mandatory (Constitution VI.4): retrying the same key replays the
 * original posting instead of moving stock twice.
 */
export async function postTransferToWip(orderId: string, companyId: string): Promise<ManufacturingPosting> {
  const response = await apiClient.post<ManufacturingPosting>(
    `/v1/workorders/${orderId}/transfer-to-wip`,
    null,
    { params: { companyId }, headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  return response.data
}

/**
 * POST /api/v1/workorders/{id}/complete - InProcess -> Completed (MF-03 voucher).
 * Same idempotency contract as the transfer (spec MF-04 replay safety).
 */
export async function postCompleteManufacture(
  orderId: string,
  companyId: string,
  producedQuantity: number,
): Promise<ManufacturingPosting> {
  const response = await apiClient.post<ManufacturingPosting>(
    `/v1/workorders/${orderId}/complete`,
    null,
    {
      params: { companyId, producedQuantity },
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    },
  )
  return response.data
}

/**
 * POST /api/v1/workorders/{id}/cancel - Submitted/InProcess -> Cancelled (MF-05), with a
 * compensating WIP -> Stores voucher when materials were issued.
 */
export async function postCancelWorkOrder(orderId: string, companyId: string): Promise<WorkOrder> {
  const response = await apiClient.post<WorkOrder>(
    `/v1/workorders/${orderId}/cancel`,
    null,
    { params: { companyId }, headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  return response.data
}
