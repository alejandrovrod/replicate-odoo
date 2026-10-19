import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../api/client'
import { useApiList, type QueryStatus } from '../../lib/useApiList'
import type {
  EmployeeInput,
  HrEmployee,
  PayrollRun,
  PayrollRunDetail,
  PayrollSubmitResult,
  SalaryComponent,
  SalarySlipPrintable,
  SalaryStructure,
  StructureAssignment,
} from './types'

export type { QueryStatus }

/** Local error wrapper for the single-read detail hook below (list hooks use the shared loader). */
const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

/** GET /api/v1/hr/employees?companyId=&page=&pageSize= - the directory rows. */
export function useEmployees(companyId: string, page = 1, pageSize = 50) {
  return useApiList<HrEmployee>('/v1/hr/employees', { companyId, page, pageSize }, Boolean(companyId))
}

/** GET /api/v1/hr/salary-components?companyId=&page=&pageSize= - the pay elements. */
export function useSalaryComponents(companyId: string, page = 1, pageSize = 50) {
  return useApiList<SalaryComponent>('/v1/hr/salary-components', { companyId, page, pageSize }, Boolean(companyId))
}

/** GET /api/v1/hr/salary-structures?companyId=&page=&pageSize= - structures with their priced lines. */
export function useSalaryStructures(companyId: string, page = 1, pageSize = 50) {
  return useApiList<SalaryStructure>('/v1/hr/salary-structures', { companyId, page, pageSize }, Boolean(companyId))
}

/** GET /api/v1/hr/structure-assignments?companyId=&page=&pageSize= - the eligibility windows. */
export function useStructureAssignments(companyId: string, page = 1, pageSize = 50) {
  return useApiList<StructureAssignment>(
    '/v1/hr/structure-assignments',
    { companyId, page, pageSize },
    Boolean(companyId),
  )
}

/** GET /api/v1/payroll-runs?companyId=&page=&pageSize= - batch headers, newest first. */
export function usePayrollRuns(companyId: string, page = 1, pageSize = 50) {
  return useApiList<PayrollRun>('/v1/payroll-runs', { companyId, page, pageSize }, Boolean(companyId))
}

/** GET /api/v1/payroll-runs/{id}?companyId= - one batch with every slip and its lines. */
export function usePayrollRunDetail(companyId: string, entryId: string | null) {
  const key = `${companyId}/${entryId ?? ''}`
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<{
    key: string
    status: 'success' | 'error'
    data: PayrollRunDetail | null
    error: ApiError | null
  } | null>(null)

  useEffect(() => {
    if (!companyId || !entryId) return undefined

    let cancelled = false
    apiClient
      .get<PayrollRunDetail>(`/v1/payroll-runs/${entryId}`, { params: { companyId } })
      .then(
        (response) => {
          if (!cancelled) setState({ key, status: 'success', data: response.data, error: null })
        },
        (cause: unknown) => {
          if (!cancelled) setState({ key, status: 'error', data: null, error: toApiError(cause) })
        },
      )

    return () => {
      cancelled = true
    }
  }, [companyId, entryId, key, attempt])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  if (!entryId) return { data: null, status: 'idle' as const, error: null, reload }
  if (state && state.key === key) {
    return { data: state.data, status: state.status, error: state.error, reload }
  }
  return { data: null, status: 'loading' as const, error: null, reload }
}

/** First/last day of the current month (UTC) for the workbench period picker defaults. */
export function payrollPeriodDefaults(): { start: string; end: string } {
  const now = new Date()
  const start = `${now.toISOString().slice(0, 7)}-01`
  const end = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 0))
    .toISOString()
    .slice(0, 10)
  return { start, end }
}

/**
 * POST /api/v1/payroll-runs/submit - one atomic batch (slips + accrual voucher).
 * `Idempotency-Key` is mandatory (Constitution VI.4): every call mints a FRESH key so a
 * retry never double-pays; replaying a key returns the stored run instead.
 */
export async function postSubmitPayrollRun(
  companyId: string,
  startDate: string,
  endDate: string,
  postingDate: string,
): Promise<PayrollSubmitResult> {
  const response = await apiClient.post<PayrollSubmitResult>(
    '/v1/payroll-runs/submit',
    { companyId, startDate, endDate, postingDate, paymentDayOverrides: null },
    { headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  return response.data
}

/**
 * POST /api/v1/payroll-runs/{id}/disburse - Submitted -> Paid (Dr 2150 / Cr bank).
 * Same idempotency contract as the submit (spec HR-02 Phase 2).
 */
export async function postDisbursePayrollRun(
  entryId: string,
  companyId: string,
  bankAccountId: string,
  postingDate: string,
): Promise<PayrollRun> {
  const response = await apiClient.post<PayrollRun>(
    `/v1/payroll-runs/${entryId}/disburse`,
    null,
    {
      params: { companyId, bankAccountId, postingDate },
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    },
  )
  return response.data
}

/**
 * POST /api/v1/payroll-runs/{id}/cancel - Submitted -> Cancelled (accrual mirror).
 * Same idempotency contract (the mirror posts GLEntry rows).
 */
export async function postCancelPayrollRun(
  entryId: string,
  companyId: string,
  postingDate: string,
): Promise<PayrollRun> {
  const response = await apiClient.post<PayrollRun>(
    `/v1/payroll-runs/${entryId}/cancel`,
    null,
    {
      params: { companyId, postingDate },
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    },
  )
  return response.data
}

/**
 * GET /api/v1/payroll-runs/{runId}/slips/{slipId}/printable?companyId= - one slip as a formal
 * pay stub (earnings/deductions, settlement date, company fiscal header). Single-read hook
 * (mirrors the run-detail loader) so the printable modal can render, retry and clear cleanly.
 */
export function useSalarySlipPrintable(
  companyId: string,
  entryId: string | null,
  slipId: string | null,
) {
  const key = `${companyId}/${entryId ?? ''}/${slipId ?? ''}`
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<{
    key: string
    status: 'success' | 'error'
    data: SalarySlipPrintable | null
    error: ApiError | null
  } | null>(null)

  useEffect(() => {
    if (!companyId || !entryId || !slipId) return undefined

    let cancelled = false
    apiClient
      .get<SalarySlipPrintable>(`/v1/payroll-runs/${entryId}/slips/${slipId}/printable`, {
        params: { companyId },
      })
      .then(
        (response) => {
          if (!cancelled) setState({ key, status: 'success', data: response.data, error: null })
        },
        (cause: unknown) => {
          if (!cancelled) setState({ key, status: 'error', data: null, error: toApiError(cause) })
        },
      )

    return () => {
      cancelled = true
    }
  }, [companyId, entryId, slipId, key, attempt])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  if (!entryId || !slipId) return { data: null, status: 'idle' as const, error: null, reload }
  if (state && state.key === key) {
    return { data: state.data, status: state.status, error: state.error, reload }
  }
  return { data: null, status: 'loading' as const, error: null, reload }
}

/**
 * POST /api/v1/hr/employees - registers one employee; when `salaryStructureId` is set the
 * initial structure assignment is created atomically. `Idempotency-Key` is injected by the
 * shared client interceptor (Article VI.4).
 */
export async function postEmployee(companyId: string, input: EmployeeInput): Promise<HrEmployee> {
  const response = await apiClient.post<HrEmployee>('/v1/hr/employees', { companyId, ...input })
  return response.data
}

/**
 * PUT /api/v1/hr/employees/{id} - updates one employee, echoing the base64 `rowVersion` token so
 * a stale write fails with a 409 `concurrency_conflict` instead of silently overwriting.
 */
export async function putEmployee(
  employeeId: string,
  companyId: string,
  rowVersion: string,
  input: EmployeeInput,
): Promise<HrEmployee> {
  const response = await apiClient.put<HrEmployee>(`/v1/hr/employees/${employeeId}`, {
    companyId,
    rowVersion,
    ...input,
  })
  return response.data
}
