import { apiClient } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { useApiList, type PagedList } from '../../../lib/useApiList'

/**
 * Mirrors `FiscalYearDto` (R-13 plan.md §4) as serialized by System.Text.Json:
 * camelCase properties, `DateOnly` as `yyyy-MM-dd`, `RowVersion` (byte[]) as base64.
 */
export interface FiscalYear {
  id: string
  companyId: string
  yearName: string
  startDate: string
  endDate: string
  isClosed: boolean
  closedAt: string | null
  /** Base64 rowversion token from GET; echoed back on close for optimistic concurrency. */
  rowVersion?: string
}

export type FiscalYearStatus = 'idle' | 'loading' | 'success' | 'error'

export interface FiscalYearFilters {
  /** `undefined` = all years; `true` = closed only; `false` = open only. */
  isClosed?: boolean
  page?: number
  pageSize?: number
}

/**
 * Paged fiscal-year master list scoped to the current company (R-13 Task 5.1,
 * Standard Pagination Pattern via `useApiList`).
 */
export function useFiscalYears(filters: FiscalYearFilters = {}): PagedList<FiscalYear> {
  const companyId = useTenantStore((s) => s.companyId)
  return useApiList<FiscalYear>(
    '/v1/fiscal-years',
    {
      companyId: companyId || undefined,
      isClosed: filters.isClosed,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 50,
    },
    Boolean(companyId),
  )
}

/** POST body (mirrors `CreateFiscalYearCommand`): `StartDate < EndDate` + no overlap. */
export interface CreateFiscalYearPayload {
  companyId: string
  yearName: string
  startDate: string
  endDate: string
}

/**
 * Creates one OPEN fiscal year. The caller supplies the `Idempotency-Key` so a
 * retried create after a network break replays instead of duplicating.
 */
export async function createFiscalYear(
  payload: CreateFiscalYearPayload,
  idempotencyKey: string = crypto.randomUUID(),
): Promise<FiscalYear> {
  const response = await apiClient.post<FiscalYear>('/v1/fiscal-years', payload, {
    headers: { 'Idempotency-Key': idempotencyKey },
  })
  return response.data
}

/**
 * Hard-locks the year (`IsClosed = 1`, terminal — re-open does not exist).
 * Requires the current `rowVersion`; a stale token resolves to 409
 * `concurrency_conflict` instead of silently winning.
 */
export async function closeFiscalYear(
  id: string,
  payload: { companyId: string; rowVersion?: string },
  idempotencyKey: string = crypto.randomUUID(),
): Promise<FiscalYear> {
  const response = await apiClient.post<FiscalYear>(
    `/v1/fiscal-years/${id}/close`,
    { companyId: payload.companyId, rowVersion: payload.rowVersion },
    { headers: { 'Idempotency-Key': idempotencyKey } },
  )
  return response.data
}
