import { Suspense, lazy, useCallback, useEffect, useState } from 'react'
import {
  CheckCircle2,
  Landmark,
  Sparkles,
  UploadCloud,
} from 'lucide-react'
import { ApiError, apiClient } from '../../api/client'
import { useErpAction } from '../../lib/useErpAction'
import { useTenantStore } from '../../store/useTenantStore'
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
  const companyId = useTenantStore((state) => state.companyId)
  const [showImporter, setShowImporter] = useState(false)
  const [refreshSignal, setRefreshSignal] = useState(0)
  const [counts, setCounts] = useState<StatusCounts>({ unreconciled: 0, matched: 0, reconciled: 0 })
  const [countsError, setCountsError] = useState<string | null>(null)

  const refresh = useCallback(() => setRefreshSignal((n) => n + 1), [])

  useEffect(() => {
    if (!companyId) return
    let cancelled = false
    apiClient
      .get<BankTransaction[]>('/v1/bank-transactions', { params: { companyId } })
      .then(
        (response) => {
          if (cancelled) return
          setCounts({
            unreconciled: response.data.filter((l) => l.status === 'Unreconciled').length,
            matched: response.data.filter((l) => l.status === 'Matched').length,
            reconciled: response.data.filter((l) => l.status === 'Reconciled').length,
          })
          setCountsError(null)
        },
        (cause: unknown) => {
          if (!cancelled) {
            setCountsError(
              cause instanceof ApiError ? cause.message : 'Failed to load staging summary.',
            )
          }
        },
      )
    return () => {
      cancelled = true
    }
  }, [companyId, refreshSignal])

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
      name: 'Unreconciled lines',
      detail: 'Awaiting a rule match or a manual reconcile',
      value: counts.unreconciled,
      resolved: counts.unreconciled === 0,
    },
    {
      name: 'Matched suggestions',
      detail: 'Rule engine proposals awaiting confirmation',
      value: counts.matched,
      resolved: counts.matched === 0,
    },
    {
      name: 'Reconciled lines',
      detail: 'Cleared with a $0.00 difference',
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
            <h2 className="text-lg font-bold text-slate-900">Banking Subsystem (ERPNext Parity)</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">
            Import bank statements, run automated heuristic matching rules, and reconcile transactions with GL vouchers.
          </p>
          {rulesState.isSuccess && rulesState.data && (
            <p className="mt-1 text-xs font-semibold text-emerald-700">
              Rules engine matched {rulesState.data.matchedCount} line(s).
            </p>
          )}
          {rulesState.error && (
            <p className="mt-1 text-xs font-semibold text-rose-700">
              {rulesState.error}{rulesState.errorCode ? ` (${rulesState.errorCode})` : ''}
            </p>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => setShowImporter((v) => !v)}
            className="flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50"
          >
            <UploadCloud className="h-4 w-4 text-sky-600" />
            Import Statement (CSV/OFX)
          </button>
          <button
            type="button"
            disabled={isRunningRules}
            onClick={() => runRules()}
            className="flex items-center gap-1.5 rounded-lg bg-sky-600 px-3 py-2 text-xs font-semibold text-white shadow-xs hover:bg-sky-700 disabled:opacity-50"
          >
            <Sparkles className="h-4 w-4" />
            {isRunningRules ? 'Running…' : 'Run Rules Engine'}
          </button>
        </div>
      </div>

      {showImporter && (
        <Suspense fallback={<p className="text-xs text-slate-500">Loading importer…</p>}>
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
                  <span className="text-xs text-slate-400">Staging lines</span>
                  <p className="text-xl font-bold text-slate-900">{card.value}</p>
                </div>
                <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                  <CheckCircle2 className="h-4 w-4" /> Staging Isolation Active
                </span>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Dual-sided reconciliation workbench */}
      <Suspense fallback={<p className="text-xs text-slate-500">Loading reconciliation workbench…</p>}>
        <BankReconciliation refreshSignal={refreshSignal} />
      </Suspense>
    </div>
  )
}
