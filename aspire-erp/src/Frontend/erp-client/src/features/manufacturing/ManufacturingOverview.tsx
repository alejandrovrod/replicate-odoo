import { Suspense, lazy, useCallback, useEffect, useState } from 'react'
import { CheckCircle2, Factory } from 'lucide-react'
import { ApiError, apiClient } from '../../api/client'
import { useTenantStore } from '../../store/useTenantStore'
import type { WorkOrder } from './types'

const BomEditor = lazy(() => import('./BomEditor').then((m) => ({ default: m.BomEditor })))
const WorkOrdersBoard = lazy(() =>
  import('./WorkOrdersBoard').then((m) => ({ default: m.WorkOrdersBoard })),
)

interface StatusCounts {
  draft: number
  submitted: number
  inProcess: number
  completed: number
}

/**
 * Manufacturing overview (Task 9.5): live work-order data behind the original layout -
 * the cards show live status counts, the execution board below runs the status flow
 * (submit / transfer / complete / cancel), and the BOM Studio shows the recipe tree.
 */
export function ManufacturingOverview() {
  const companyId = useTenantStore((state) => state.companyId)
  const [refreshSignal, setRefreshSignal] = useState(0)
  const [counts, setCounts] = useState<StatusCounts>({ draft: 0, submitted: 0, inProcess: 0, completed: 0 })
  const [countsError, setCountsError] = useState<string | null>(null)

  const refresh = useCallback(() => setRefreshSignal((n) => n + 1), [])

  useEffect(() => {
    if (!companyId) return
    let cancelled = false
    apiClient.get<WorkOrder[]>('/v1/workorders', { params: { companyId } }).then(
      (response) => {
        if (cancelled) return
        setCounts({
          draft: response.data.filter((o) => o.status === 'Draft').length,
          submitted: response.data.filter((o) => o.status === 'Submitted').length,
          inProcess: response.data.filter((o) => o.status === 'InProcess').length,
          completed: response.data.filter((o) => o.status === 'Completed').length,
        })
        setCountsError(null)
      },
      (cause: unknown) => {
        if (!cancelled) {
          setCountsError(
            cause instanceof ApiError ? cause.message : 'Failed to load production summary.',
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
      name: 'Draft orders',
      detail: 'Awaiting submission against an active default BOM',
      value: counts.draft,
      resolved: counts.draft === 0,
    },
    {
      name: 'Submitted orders',
      detail: 'Authorized - materials ready for WIP issue (MF-02)',
      value: counts.submitted,
      resolved: counts.submitted === 0,
    },
    {
      name: 'In process',
      detail: 'Materials in WIP - awaiting completion (MF-03)',
      value: counts.inProcess,
      resolved: false,
    },
    {
      name: 'Completed',
      detail: 'Finished goods capitalized into inventory',
      value: counts.completed,
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
              <Factory className="h-4 w-4" />
            </span>
            <h2 className="text-lg font-bold text-slate-900">Manufacturing & Shop Floor (ERPNext Parity)</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">
            Run work orders from submission through WIP transfer to completion, with BOM cost roll-ups.
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
      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        {countsError ? (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-5 text-xs text-rose-700 md:col-span-4">
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
                  <span className="text-xs text-slate-400">Work orders</span>
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

      {/* Execution board (keyed by the banner refresh: a refresh remounts both panels). */}
      <Suspense fallback={<p className="text-xs text-slate-500">Loading execution board…</p>}>
        <WorkOrdersBoard key={`wo-${refreshSignal}`} companyId={companyId} />
      </Suspense>

      {/* BOM Studio */}
      <Suspense fallback={<p className="text-xs text-slate-500">Loading BOM Studio…</p>}>
        <BomEditor key={`bom-${refreshSignal}`} companyId={companyId} />
      </Suspense>
    </div>
  )
}
