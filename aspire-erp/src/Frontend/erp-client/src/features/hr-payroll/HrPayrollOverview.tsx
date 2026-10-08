import { CheckCircle2, Users } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Suspense, lazy, useCallback, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { MAX_PAGE_SIZE } from '../../lib/pagination'
import { useTenantStore } from '../../store/useTenantStore'
import { usePayrollRuns, payrollPeriodDefaults } from './useHrPayrollData'
import { Button } from '../../components/ui/Button'

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
  const { t } = useTranslation('hr-payroll')
  const companyId = useTenantStore((state) => state.companyId)
  const [refreshSignal, setRefreshSignal] = useState(0)
  const [period, setPeriod] = useState(payrollPeriodDefaults)

  // Status cards need per-status counts, and the list endpoint has no status filter: one
  // bounded read (MAX_PAGE_SIZE, no pager) feeds the counters. Beyond 500 runs the cards
  // saturate — a dedicated counts endpoint would lift that ceiling.
  const countsQuery = usePayrollRuns(companyId, 1, MAX_PAGE_SIZE)
  const { reload: reloadCounts } = countsQuery
  const counts = useMemo<StatusCounts>(() => {
    const rows = countsQuery.status === 'success' ? countsQuery.items : []
    return {
      submitted: rows.filter((r) => r.status === 'Submitted').length,
      paid: rows.filter((r) => r.status === 'Paid').length,
      cancelled: rows.filter((r) => r.status === 'Cancelled').length,
    }
  }, [countsQuery])
  const countsError = countsQuery.status === 'error' ? countsQuery.error : null

  const refresh = useCallback(() => {
    setRefreshSignal((n) => n + 1)
    reloadCounts()
  }, [reloadCounts])
  const onPeriodChange = useCallback((start: string, end: string) => {
    setPeriod({ start, end })
  }, [])

  const cards = [
    {
      name: t('cards.submittedName'),
      detail: t('cards.submittedDetail'),
      value: counts.submitted,
      resolved: counts.submitted === 0,
    },
    {
      name: t('cards.paidName'),
      detail: t('cards.paidDetail'),
      value: counts.paid,
      resolved: true,
    },
    {
      name: t('cards.cancelledName'),
      detail: t('cards.cancelledDetail'),
      value: counts.cancelled,
      resolved: true,
    },
  ]

  return (
    <div className="space-y-6">
      {/* Top Banner & Actions */}
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-gradient-to-r from-sky-50 to-white p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-sky-600 text-white shadow-xs">
              <Users className="h-4 w-4" />
            </span>
            <h2 className="text-lg font-bold text-slate-900">{t('overview.title')}</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">{t('overview.subtitle')}</p>
          <Button
            variant="secondary"
            size="sm"
            onClick={refresh}
            className="mt-2"
          >
            {t('overview.refresh')}
          </Button>
        </div>
      </div>

      {/* Live status cards */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        {countsError ? (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-5 text-xs text-rose-700 md:col-span-3">
            {countsError instanceof ApiError ? countsError.message : t('overview.loadFailed')}
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
                  <span className="text-xs text-slate-400">{t('cards.runs')}</span>
                  <p className="text-xl font-bold text-slate-900">{card.value}</p>
                </div>
                <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                  <CheckCircle2 className="h-4 w-4" /> {t('cards.live')}
                </span>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Payroll workbench (keyed by the banner refresh: a refresh remounts both panels). */}
      <Suspense fallback={<p className="text-xs text-slate-500">{t('overview.loadingWorkbench')}</p>}>
        <PayrollWorkbench
          key={`pay-${refreshSignal}`}
          companyId={companyId}
          periodStart={period.start}
          periodEnd={period.end}
          onPeriodChange={onPeriodChange}
        />
      </Suspense>

      {/* Employee directory (eligibility badges follow the workbench period). */}
      <Suspense fallback={<p className="text-xs text-slate-500">{t('overview.loadingDirectory')}</p>}>
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
