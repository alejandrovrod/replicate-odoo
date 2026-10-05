import { Suspense, lazy, useEffect, useState } from 'react'
import { Handshake } from 'lucide-react'
import { ApiError } from '../../api/client'
import { crmApi } from './api/crmApi'
import type { LeadDto } from './types/crm'

const OpportunityKanbanBoard = lazy(() =>
  import('./components/OpportunityKanbanBoard').then((m) => ({
    default: m.OpportunityKanbanBoard,
  })),
)

/**
 * CRM overview (fix-pass C1): the reachable home of the sales pipeline - a live leads
 * strip above the lazily loaded Kanban board (the ManufacturingOverview lazy-board
 * precedent, which is what emits the dedicated CRM chunk).
 */
export function CrmOverview() {
  const [leads, setLeads] = useState<LeadDto[]>([])
  const [leadsError, setLeadsError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    crmApi.getLeads().then(
      (rows) => {
        if (cancelled) return
        setLeads(rows)
        setLeadsError(null)
      },
      (cause: unknown) => {
        if (!cancelled) {
          setLeadsError(
            cause instanceof ApiError ? cause.message : 'Failed to load leads.',
          )
        }
      },
    )
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-emerald-100 bg-gradient-to-r from-emerald-50 to-sky-50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-600 text-white shadow-xs">
              <Handshake className="h-4 w-4" />
            </span>
            <h2 className="text-lg font-bold text-slate-900">CRM & Sales Pipeline (ERPNext Parity)</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">
            Leads in, qualification, pipeline drag-drop, 1-click sales orders on ClosedWon deals.
          </p>
        </div>
        <div className="text-right">
          <p className="text-xs font-medium text-slate-500">Open leads</p>
          <p className="text-2xl font-bold text-emerald-700">{leads.length}</p>
        </div>
      </div>

      {leadsError ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-5 text-xs text-rose-700">
          {leadsError}
        </div>
      ) : null}

      <Suspense fallback={<p className="text-xs text-slate-500">Loading pipeline board…</p>}>
        <OpportunityKanbanBoard />
      </Suspense>
    </div>
  )
}
