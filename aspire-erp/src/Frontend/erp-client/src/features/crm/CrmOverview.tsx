import { Suspense, lazy, useEffect, useState } from 'react'
import { Handshake } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import { crmApi } from './api/crmApi'

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
  const { t } = useTranslation('crm')
  // The strip shows only the COUNT, so it reads totalCount from a 1-row page instead of
  // pulling the whole list (Standard Pagination Pattern).
  const [leadCount, setLeadCount] = useState(0)
  // Kept as the raw error, not a string: the ApiError branch is already localized by the
  // backend (Accept-Language) while the generic branch must re-render when the user switches
  // language - a pre-rendered string would stay frozen in the old language.
  const [leadsError, setLeadsError] = useState<unknown>(null)

  useEffect(() => {
    let cancelled = false
    crmApi.getLeads(1, 1).then(
      (page) => {
        if (cancelled) return
        setLeadCount(page.totalCount)
        setLeadsError(null)
      },
      (cause: unknown) => {
        if (!cancelled) setLeadsError(cause)
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
            <h2 className="text-lg font-bold text-slate-900">{t('overview.title')}</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">{t('overview.subtitle')}</p>
        </div>
        <div className="text-right">
          <p className="text-xs font-medium text-slate-500">{t('overview.openLeads')}</p>
          <p className="text-2xl font-bold text-emerald-700">{leadCount}</p>
        </div>
      </div>

      {leadsError ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-5 text-xs text-rose-700">
          {leadsError instanceof ApiError ? leadsError.message : t('overview.loadLeadsFailed')}
        </div>
      ) : null}

      <Suspense fallback={<p className="text-xs text-slate-500">{t('overview.loadingBoard')}</p>}>
        <OpportunityKanbanBoard />
      </Suspense>
    </div>
  )
}
