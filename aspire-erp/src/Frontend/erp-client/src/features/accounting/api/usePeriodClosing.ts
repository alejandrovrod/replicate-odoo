import { useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { useApiList, type PagedList } from '../../../lib/useApiList'

/** Lifecycle of a closing voucher: `Draft → Submitted → Cancelled` only (spec FC-05). */
export type ClosingDocumentStatus = 'Draft' | 'Submitted' | 'Cancelled'

/**
 * Mirrors `PeriodClosingVoucherDto` (R-13 plan.md §4, extended per spec §7) as serialized
 * by System.Text.Json: camelCase, `DateOnly` as `yyyy-MM-dd`, `RowVersion` as base64.
 */
export interface PeriodClosingVoucher {
  id: string
  companyId: string
  fiscalYearId: string
  voucherNo: string
  postingDate: string
  retainedEarningsAccountId: string
  documentStatus: ClosingDocumentStatus
  remarks: string | null
  idempotencyKey?: string | null
  /** Base64 rowversion token from GET; echoed back on submit/cancel. */
  rowVersion?: string
}

/** One read-only preview line (mirrors `ClosingPreviewLineDto`, plan.md §4). */
export interface ClosingPreviewLine {
  accountId: string
  code: string
  name: string
  rootType: string
  debit: number
  credit: number
  balance: number
}

/** Preview envelope (mirrors `ClosingPreviewDto`): what submit would post, plus net P&L. */
export interface ClosingPreview {
  companyId: string
  fiscalYearId: string
  lines: ClosingPreviewLine[]
  retainedLine: ClosingPreviewLine | null
  net: number
}

export interface PeriodClosingFilters {
  fiscalYearId?: string
  status?: ClosingDocumentStatus
  page?: number
  pageSize?: number
}

/**
 * Paged closing-voucher execution list scoped to the current company
 * (R-13 Task 5.2, Standard Pagination Pattern via `useApiList`).
 */
export function usePeriodClosingVouchers(
  filters: PeriodClosingFilters = {},
): PagedList<PeriodClosingVoucher> {
  const companyId = useTenantStore((s) => s.companyId)
  return useApiList<PeriodClosingVoucher>(
    '/v1/period-closing-vouchers',
    {
      companyId: companyId || undefined,
      fiscalYearId: filters.fiscalYearId || undefined,
      status: filters.status || undefined,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 50,
    },
    Boolean(companyId),
  )
}

/**
 * Backwards-compatible alias: the unfiltered company list.
 * Prefer `usePeriodClosingVouchers` with explicit filters in new code.
 */
export function usePeriodClosing(
  page: number = 1,
  pageSize: number = 50,
): PagedList<PeriodClosingVoucher> {
  return usePeriodClosingVouchers({ page, pageSize })
}

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

export type PreviewStatus = 'idle' | 'loading' | 'success' | 'error'

/**
 * Read-only pre-submit P&L preview (`GET …/unclosed-balances`): the exact line set
 * submit would post, from the same balance query (plan.md §4). No writes.
 */
export function useClosingPreview(fiscalYearId: string | null) {
  const companyId = useTenantStore((s) => s.companyId)
  const [preview, setPreview] = useState<ClosingPreview | null>(null)
  const [status, setStatus] = useState<PreviewStatus>('idle')
  const [error, setError] = useState<ApiError | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!companyId || !fiscalYearId) {
      setPreview(null)
      setStatus('idle')
      setError(null)
      return
    }
    let cancelled = false
    setStatus('loading')
    apiClient
      .get<ClosingPreview>('/v1/period-closing-vouchers/unclosed-balances', {
        params: { companyId, fiscalYearId },
      })
      .then(
        (response) => {
          if (cancelled) return
          setPreview(response.data)
          setError(null)
          setStatus('success')
        },
        (cause: unknown) => {
          if (cancelled) return
          setPreview(null)
          setError(toApiError(cause))
          setStatus('error')
        },
      )
    return () => {
      cancelled = true
    }
  }, [companyId, fiscalYearId, attempt])

  return {
    preview,
    status,
    error,
    reload: () => setAttempt((n) => n + 1),
  }
}

/** POST body (mirrors `CreatePeriodClosingVoucherCommand`): Draft bound to one open FY. */
export interface CreateClosingVoucherPayload {
  companyId: string
  fiscalYearId: string
  postingDate: string
  /** Omitted → the server resolves `Company.DefaultRetainedEarningsAccountId` (FC-03). */
  retainedEarningsAccountId?: string
  remarks?: string
}

const idempotencyHeader = (key: string) => ({ headers: { 'Idempotency-Key': key } })

/** Creates one `Draft` voucher (no GL impact yet). */
export async function createClosingVoucher(
  payload: CreateClosingVoucherPayload,
  idempotencyKey: string = crypto.randomUUID(),
): Promise<PeriodClosingVoucher> {
  const response = await apiClient.post<PeriodClosingVoucher>(
    '/v1/period-closing-vouchers',
    {
      companyId: payload.companyId,
      fiscalYearId: payload.fiscalYearId,
      postingDate: payload.postingDate,
      retainedEarningsAccountId: payload.retainedEarningsAccountId || null,
      remarks: payload.remarks ?? null,
    },
    idempotencyHeader(idempotencyKey),
  )
  return response.data
}

/**
 * Submits one Draft voucher (atomic balanced close, FC-01/02/03/05).
 * Reuse the SAME `idempotencyKey` across retries/double-clicks: a replayed submit
 * after commit returns the recorded success with zero new GL rows (FC-09).
 */
export async function submitClosingVoucher(
  id: string,
  args: { companyId: string; rowVersion?: string },
  idempotencyKey: string = crypto.randomUUID(),
): Promise<PeriodClosingVoucher> {
  const response = await apiClient.post<PeriodClosingVoucher>(
    `/v1/period-closing-vouchers/${id}/submit?companyId=${encodeURIComponent(args.companyId)}`,
    { rowVersion: args.rowVersion },
    idempotencyHeader(idempotencyKey),
  )
  return response.data
}

/**
 * Cancels one Submitted voucher by appending the compensating reversal
 * (originals untouched, FC-06). Refused when the fiscal year is closed.
 */
export async function cancelClosingVoucher(
  id: string,
  args: { companyId: string; rowVersion?: string },
  idempotencyKey: string = crypto.randomUUID(),
): Promise<PeriodClosingVoucher> {
  const response = await apiClient.post<PeriodClosingVoucher>(
    `/v1/period-closing-vouchers/${id}/cancel?companyId=${encodeURIComponent(args.companyId)}`,
    { rowVersion: args.rowVersion },
    idempotencyHeader(idempotencyKey),
  )
  return response.data
}
