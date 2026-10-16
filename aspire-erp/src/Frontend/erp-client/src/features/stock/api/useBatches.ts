import { useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'

/** One batch master (mirrors `BatchDto`). */
export interface Batch {
  id: string
  itemId: string
  batchNo: string
  manufacturingDate?: string | null
  expiryDate?: string | null
  isDisabled: boolean
  supplierBatch?: string | null
  description?: string | null
}

/** One FEFO availability row (mirrors `BatchAvailabilityDto`). */
export interface BatchAvailability {
  batchId: string
  batchNo: string
  manufacturingDate?: string | null
  expiryDate?: string | null
  isDisabled: boolean
  qty: number
  isExpired: boolean
}

/** One serial master (mirrors `SerialNoDto`). */
export interface SerialNo {
  id: string
  itemId: string
  serialNumber: string
  status: string
  batchId?: string | null
  batchNo?: string | null
  currentWarehouseId?: string | null
  purchaseDate?: string | null
  purchaseDocumentNo?: string | null
  deliveryDate?: string | null
  deliveryDocumentNo?: string | null
}

interface QueryState<T> {
  data: T
  status: 'idle' | 'loading' | 'success' | 'error'
  error: ApiError | null
  reload: () => void
}

function useGetArray<T>(url: string | null, paramsRecord: Record<string, string>): QueryState<T[]> {
  // Same params-keying contract as useApiList: the object identity changes per render,
  // so the effect keys off its JSON round-trip instead.
  const paramsKey = JSON.stringify(paramsRecord)
  const [attempt, setAttempt] = useState(0)
  const [data, setData] = useState<T[]>([])
  const [status, setStatus] = useState<QueryState<T[]>['status']>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    if (!url) {
      setData([])
      setStatus('idle')
      setError(null)
      return undefined
    }
    let cancelled = false
    setStatus('loading')
    setError(null)
    const query = JSON.parse(paramsKey) as Record<string, string>
    apiClient
      .get<T[]>(url, { params: query })
      .then(
        (response) => {
          if (!cancelled) {
            setData(response.data)
            setStatus('success')
          }
        },
        (cause: unknown) => {
          if (!cancelled) {
            setData([])
            setError(
              cause instanceof ApiError
                ? cause
                : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause)),
            )
            setStatus('error')
          }
        },
      )
    return () => {
      cancelled = true
    }
  }, [url, paramsKey, attempt])

  return { data, status, error, reload: () => setAttempt((c) => c + 1) }
}

/**
 * FEFO-ordered availability of one item in one warehouse
 * (GET /api/v1/batches/availability). The picking/issue source: nearest expiry first,
 * zero balances excluded, expired rows flagged (never dispatchable).
 */
export function useBatchAvailability(itemId: string, warehouseId: string) {
  const query = useGetArray<BatchAvailability>(
    itemId && warehouseId ? '/v1/batches/availability' : null,
    { itemId, warehouseId },
  )
  return { batches: query.data, status: query.status, error: query.error, reload: query.reload }
}

/** Active serials of one item in one warehouse (GET /api/v1/serialnumbers). */
export function useSerials(itemId: string, warehouseId: string) {
  const query = useGetArray<SerialNo>(itemId && warehouseId ? '/v1/serialnumbers' : null, {
    itemId,
    warehouseId,
  })
  return { serials: query.data, status: query.status, error: query.error, reload: query.reload }
}

/** POST /api/v1/batches body (mirrors `CreateBatchCommand`). */
export async function createBatch(payload: {
  itemId: string
  batchNo: string
  manufacturingDate?: string | null
  expiryDate?: string | null
}): Promise<Batch> {
  const response = await apiClient.post<Batch>('/v1/batches', {
    itemId: payload.itemId,
    batchNo: payload.batchNo,
    manufacturingDate: payload.manufacturingDate ?? null,
    expiryDate: payload.expiryDate ?? null,
  })
  return response.data
}
