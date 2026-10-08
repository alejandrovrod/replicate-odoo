import { Suspense, lazy, useCallback, useMemo, useState } from 'react'
import {
  CheckCircle2,
  Landmark,
  Sparkles,
  UploadCloud,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ApiError, apiClient } from '../../api/client'
import { useApiList } from '../../lib/useApiList'
import { MAX_PAGE_SIZE } from '../../lib/pagination'
import { useErpAction } from '../../lib/useErpAction'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { useTenantStore } from '../../store/useTenantStore'
import { Button } from '../../components/ui/Button'
import type { BankTransaction, RuleMatchSummary } from './types'

const BankStatementImporter = lazy(() =>
  import('./BankStatementImporter').then((m) => ({ default: m.BankStatementImporter })),
)
const BankReconciliation = lazy(() =>
  import('./BankReconciliation').then((m) => ({ default: m.BankReconciliation })),
)

interface StatusCounts {
  unreconciled: number
  matched: number
  reconciled: number
}

/**
 * Banking workbench (task 6.6): live staging data behind the original layout - the banner
 * actions import statements and run the heuristic rules engine, the cards show live status
 * counts, and the dual-sided grid below reconciles lines one click at a time.
 */
export function BankingOverview() {
  const { t, i18n } = useTranslation('banking')
  const companyId = useTenantStore((state) => state.companyId)
  const [showImporter, setShowImporter] = useState(false)
  const [refreshSignal, setRefreshSignal] = useState(0)

  // Status cards need per-status counts, and the list endpoint has no status filter: one
  // bounded read (MAX_PAGE_SIZE, no pager) feeds the counters. Beyond 500 staging lines the
  // cards saturate — a dedicated counts endpoint would lift that ceiling.
  const countsQuery = useApiList<BankTransaction>(
    '/v1/bank-transactions',
    { companyId, page: 1, pageSize: MAX_PAGE_SIZE },
    Boolean(companyId),
  )
  const { reload: reloadCounts } = countsQuery
  const counts = useMemo<StatusCounts>(() => {
    const rows = countsQuery.status === 'success' ? countsQuery.items : []
    return {
      unreconciled: rows.filter((l) => l.status === 'Unreconciled').length,
      matched: rows.filter((l) => l.status === 'Matched').length,
      reconciled: rows.filter((l) => l.status === 'Reconciled').length,
    }
  }, [countsQuery])
  const countsError = countsQuery.status === 'error' ? countsQuery.error : null

  const refresh = useCallback(() => {
    setRefreshSignal((n) => n + 1)
    reloadCounts()
  }, [reloadCounts])

  const { state: rulesState, dispatch: runRules, isPending: isRunningRules } = useErpAction<
    RuleMatchSummary,
    void
  >(async () => {
    const response = await apiClient.post<RuleMatchSummary>(
      '/v1/bank-transactions/run-rules',
      { companyId },
      { headers: { 'Idempotency-Key': crypto.randomUUID() } },
    )
    refresh()
    return response.data
  })

  const cards = [
    {
      name: t('cards.unreconciledName'),
      detail: t('cards.unreconciledDetail'),
      value: counts.unreconciled,
      resolved: counts.unreconciled === 0,
    },
    {
      name: t('cards.matchedName'),
      detail: t('cards.matchedDetail'),
      value: counts.matched,
      resolved: counts.matched === 0,
    },
    {
      name: t('cards.reconciledName'),
      detail: t('cards.reconciledDetail'),
      value: counts.reconciled,
      resolved: true,
    },
  ]

  return (
    <div className="space-y-6">
      {/* Top Banner & Actions */}
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-gradient-to-r from-sky-50 to-indigo-50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-sky-600 text-white shadow-xs">
              <Landmark className="h-4 w-4" />
            </span>
            <h2 className="text-lg font-bold text-slate-900">{t('overview.title')}</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">{t('overview.subtitle')}</p>
          {rulesState.isSuccess && rulesState.data && (
            <p className="mt-1 text-xs font-semibold text-emerald-700">
              {t('overview.rulesMatched', { count: rulesState.data.matchedCount })}
            </p>
          )}
          {rulesState.error && (
            <p className="mt-1 text-xs font-semibold text-rose-700">
              {translateErrorCode(i18n, rulesState.errorCode, rulesState.error)}
            </p>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setShowImporter((v) => !v)}
            className="flex items-center gap-1.5"
          >
            <UploadCloud className="h-4 w-4" />
            {t('overview.importStatement')}
          </Button>
          <Button
            variant="default"
            size="sm"
            disabled={isRunningRules}
            onClick={() => runRules()}
            className="flex items-center gap-1.5"
          >
            <Sparkles className="h-4 w-4" />
            {isRunningRules ? t('overview.running') : t('overview.runRules')}
          </Button>
        </div>
      </div>

      {showImporter && (
        <Suspense fallback={<p className="text-xs text-slate-500">{t('overview.loadingImporter')}</p>}>
          <BankStatementImporter
            onImported={() => {
              setShowImporter(false)
              refresh()
            }}
          />
        </Suspense>
      )}

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
                  <span className="text-xs text-slate-400">{t('cards.stagingLines')}</span>
                  <p className="text-xl font-bold text-slate-900">{card.value}</p>
                </div>
                <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                  <CheckCircle2 className="h-4 w-4" /> {t('cards.isolationActive')}
                </span>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Dual-sided reconciliation workbench */}
      <Suspense fallback={<p className="text-xs text-slate-500">{t('overview.loadingWorkbench')}</p>}>
        <BankReconciliation refreshSignal={refreshSignal} />
      </Suspense>
    </div>
  )
}
