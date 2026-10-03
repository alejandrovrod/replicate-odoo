import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../api/client'
import type { GeneralLedgerReportDto } from './types'

export type GeneralLedgerStatus = 'idle' | 'loading' | 'success' | 'error'

/** Query inputs for GET /api/v1/FinancialReports/general-ledger (empty values are dropped). */
export interface GeneralLedgerFilters {
  companyId: string
  accountId?: string
  voucherId?: string
  from?: string
  to?: string
}

interface ReportResult {
  key: string
  attempt: number
  status: 'success' | 'error'
  report: GeneralLedgerReportDto
  error: ApiError | null
}

const EMPTY_REPORT: GeneralLedgerReportDto = {
  items: [],
  totalDebit: 0,
  totalCredit: 0,
  difference: 0,
}

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

/** Unset filters never reach the query string (only `companyId` is required by the contract). */
function buildParams(filters: GeneralLedgerFilters): Record<string, string> {
  const params: Record<string, string> = {}
  if (filters.companyId) params.companyId = filters.companyId
  if (filters.accountId) params.accountId = filters.accountId
  if (filters.voucherId) params.voucherId = filters.voucherId
  if (filters.from) params.from = filters.from
  if (filters.to) params.to = filters.to
  return params
}

/**
 * Loads the general-ledger report for the current filter combination (Task 2.6 contract).
 * Tenant header injection and ProblemDetails handling live in `src/api/client.ts`.
 *
 * Same status-derivation contract as `useAccountTree` / `useApiList`: the request status is
 * *derived* by comparing the loaded `(key, attempt)` against the current inputs, so a changed
 * filter reads as `loading` without a transitional render pass. The params object is a literal
 * rebuilt per render, so the effect keys off its JSON round-trip (`paramsKey`) instead of its
 * identity - keying on identity would refetch forever.
 */
export function useGeneralLedger(filters: GeneralLedgerFilters) {
  const params = buildParams(filters)
  const paramsKey = JSON.stringify(params)
  const key = `general-ledger?${paramsKey}`
  const enabled = Boolean(params.companyId)
  const [attempt, setAttempt] = useState(0)
  const [result, setResult] = useState<ReportResult | null>(null)

  useEffect(() => {
    if (!enabled) return undefined

    let cancelled = false
    const query = JSON.parse(paramsKey) as Record<string, string>
    apiClient.get<GeneralLedgerReportDto>('/v1/FinancialReports/general-ledger', { params: query }).then(
      (response) => {
        if (!cancelled) {
          setResult({ key, attempt, status: 'success', report: response.data, error: null })
        }
      },
      (cause: unknown) => {
        if (!cancelled) {
          setResult({ key, attempt, status: 'error', report: EMPTY_REPORT, error: toApiError(cause) })
        }
      },
    )

    return () => {
      cancelled = true
    }
  }, [enabled, paramsKey, attempt, key])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  if (!enabled) return { report: EMPTY_REPORT, status: 'idle' as const, error: null, reload }
  if (result && result.key === key && result.attempt === attempt) {
    return { report: result.report, status: result.status, error: result.error, reload }
  }
  return { report: EMPTY_REPORT, status: 'loading' as const, error: null, reload }
}
