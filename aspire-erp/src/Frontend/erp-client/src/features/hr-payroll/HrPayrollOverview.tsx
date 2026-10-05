import { Suspense, lazy, useCallback, useEffect, useState } from 'react'
import { CheckCircle2, Users } from 'lucide-react'
import { ApiError, apiClient } from '../../api/client'
import { useTenantStore } from '../../store/useTenantStore'
import type { PayrollRun } from './types'
import { payrollPeriodDefaults } from './useHrPayrollData'

const EmployeeDirectory = lazy(() =>
  import('./EmployeeDirectory').then((m) => ({ default: m.EmployeeDirectory })),
)
const PayrollWorkbench = lazy(() =>
  import('./PayrollWorkbench').then((m) => ({ default: m.PayrollWorkbench })),
)

interface StatusCounts {
  submitted: number
  paid: number
  cancelled: number
}

/**
 * HR & Payroll overview (Task 12.5): live batch data behind the original layout - the
 * cards show live run counts, the directory lists employees with HR-03 eligibility badges
 * for the workbench period, and the workbench below runs the submit / disburse / cancel
 * flow with slip-level detail.
 */
export function HrPayrollOverview() {
  const companyId = useTenantStore((state) => state.companyId)
  const [refreshSignal, setRefreshSignal] = useState(0)
  const [period, setPeriod] = useState(payrollPeriodDefaults)
  const [counts, setCounts] = useState<StatusCounts>({ submitted: 0, paid: 0, cancelled: 0 })
  const [countsError, setCountsError] = useState<string | null>(null)

  const refresh = useCallback(() => setRefreshSignal((n) => n + 1), [])
  const onPeriodChange = useCallback((start: string, end: string) => {
    setPeriod({ start, end })
  }, [])

  useEffect(() => {
    if (!companyId) return
    let cancelled = false
    apiClient.get<PayrollRun[]>('/v1/payroll-runs', { params: { companyId } }).then(
      (response) => {
        if (cancelled) return
        setCounts({
          submitted: response.data.filter((r) => r.status === 'Submitted').length,
          paid: response.data.filter((r) => r.status === 'Paid').length,
          cancelled: response.data.filter((r) => r.status === 'Cancelled').length,
        })
        setCountsError(null)
      },
      (cause: unknown) => {
        if (!cancelled) {
          setCountsError(
            cause instanceof ApiError ? cause.message : 'Failed to load payroll summary.',
          )
        }
      },
    )
    return () => {
      cancelled = true
    }
  }, [companyId, refreshSignal])

  const cards = [
    {
      name: 'Submitted runs',
      detail: 'Accrued — awaiting bank disbursement (HR-02 Phase 2)',
      value: counts.submitted,
      resolved: counts.submitted === 0,
    },
    {
      name: 'Paid runs',
      detail: 'Disbursed — payable cleared back to zero',
      value: counts.paid,
      resolved: true,
    },
    {
      name: 'Cancelled runs',
      detail: 'Accrual mirrored — period released for re-run',
      value: counts.cancelled,
      resolved: true,
    },
  ]

  return (
    <div className="space-y-6">
      {/* Top Banner & Actions */}
      <div className="flex flex-col gap-4 rounded-xl border border-indigo-100 bg-gradient-to-r from-indigo-50 to-sky-50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-indigo-600 text-white shadow-xs">
              <Users className="h-4 w-4" />
            </span>
            <h2 className="text-lg font-bold text-slate-900">HR & Payroll (ERPNext Parity)</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">
            Monthly payroll batches from submission through disbursement, with per-employee pay stubs.
          </p>
          <button
            type="button"
            onClick={refresh}
            className="mt-2 rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50"
          >
            Refresh boards
          </button>
        </div>
      </div>

      {/* Live status cards */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        {countsError ? (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-5 text-xs text-rose-700 md:col-span-3">
            {countsError}
          </div>
        ) : (
          cards.map((card) => (
            <div key={card.name} className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="font-semibold text-slate-900">{card.name}</h3>
                  <p className="text-xs text-slate-500">{card.detail}</p>
                </div>
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                    card.value === 0 && card.resolved
                      ? 'bg-emerald-100 text-emerald-800'
                      : 'bg-amber-100 text-amber-800'
                  }`}
                >
                  {card.value}
                </span>
              </div>

              <div className="mt-5 flex items-baseline justify-between border-t border-slate-100 pt-4">
                <div>
                  <span className="text-xs text-slate-400">Payroll runs</span>
                  <p className="text-xl font-bold text-slate-900">{card.value}</p>
                </div>
                <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                  <CheckCircle2 className="h-4 w-4" /> Live
                </span>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Payroll workbench (keyed by the banner refresh: a refresh remounts both panels). */}
      <Suspense fallback={<p className="text-xs text-slate-500">Loading payroll workbench…</p>}>
        <PayrollWorkbench
          key={`pay-${refreshSignal}`}
          companyId={companyId}
          periodStart={period.start}
          periodEnd={period.end}
          onPeriodChange={onPeriodChange}
        />
      </Suspense>

      {/* Employee directory (eligibility badges follow the workbench period). */}
      <Suspense fallback={<p className="text-xs text-slate-500">Loading employee directory…</p>}>
        <EmployeeDirectory
          key={`emp-${refreshSignal}`}
          companyId={companyId}
          periodStart={period.start}
          periodEnd={period.end}
        />
      </Suspense>
    </div>
  )
}
