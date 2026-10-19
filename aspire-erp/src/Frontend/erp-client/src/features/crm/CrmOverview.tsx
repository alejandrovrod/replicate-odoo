import { Suspense, lazy, useCallback, useEffect, useState } from 'react'
import { Handshake } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import { Button } from '../../components/ui/Button'
import { crmApi } from './api/crmApi'
import { LeadsManagementModal } from './components/LeadsManagementModal'

const OpportunityKanbanBoard = lazy(() =>
  import('./components/OpportunityKanbanBoard').then((m) => ({
    default: m.OpportunityKanbanBoard,
  })),
)

/**
 * CRM overview (fix-pass C1): the reachable home of the sales pipeline - a live leads
 * strip above the lazily loaded Kanban board (the ManufacturingOverview lazy-board
 * precedent, which is what emits the dedicated CRM chunk). The leads strip doubles as the
 * entry point of the leads manager: the interactive counter, "View Leads" and "+ New Lead"
 * all open `LeadsManagementModal`, and a conversion reloads both the counter and the board.
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
  const [isLeadsModalOpen, setIsLeadsModalOpen] = useState(false)
  const [leadsView, setLeadsView] = useState<'list' | 'form'>('list')
  const [boardReloadToken, setBoardReloadToken] = useState(0)

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

  /** After an intake/conversion: refresh the counter and make the board re-read the pipeline. */
  const handleLeadsChanged = useCallback(() => {
    crmApi.getLeads(1, 1).then(
      (page) => {
        setLeadCount(page.totalCount)
        setLeadsError(null)
      },
      (cause: unknown) => setLeadsError(cause),
    )
    setBoardReloadToken((token) => token + 1)
  }, [])

  const openLeads = useCallback((view: 'list' | 'form') => {
    setLeadsView(view)
    setIsLeadsModalOpen(true)
  }, [])

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-gradient-to-r from-sky-50 to-white p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-sky-600 text-white shadow-xs">
              <Handshake className="h-4 w-4" />
            </span>
            <h2 className="text-lg font-bold text-slate-900">{t('overview.title')}</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">{t('overview.subtitle')}</p>
        </div>

        <div className="flex items-center gap-4 sm:justify-end">
          {/* The "open leads" strip is itself a button, so the banner surface opens the manager. */}
          <button
            type="button"
            onClick={() => openLeads('list')}
            aria-label={t('leads.viewLeads', 'View Leads')}
            className="cursor-pointer rounded-lg border border-transparent px-3 py-1 text-right transition-colors hover:border-sky-200 hover:bg-sky-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500"
          >
            <span className="block text-xs font-medium text-slate-500">{t('overview.openLeads')}</span>
            <span className="block text-2xl font-bold text-sky-700">{leadCount}</span>
          </button>

          <div className="flex flex-col gap-2">
            <Button variant="outline" size="sm" onClick={() => openLeads('list')}>
              {t('leads.viewLeads', 'View Leads')}
            </Button>
            <Button size="sm" onClick={() => openLeads('form')}>
              + {t('leads.newLead', 'New Lead')}
            </Button>
          </div>
        </div>
      </div>

      {leadsError ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-5 text-xs text-rose-700">
          {leadsError instanceof ApiError ? leadsError.message : t('overview.loadLeadsFailed')}
        </div>
      ) : null}

      <Suspense fallback={<p className="text-xs text-slate-500">{t('overview.loadingBoard')}</p>}>
        <OpportunityKanbanBoard reloadToken={boardReloadToken} />
      </Suspense>

      <LeadsManagementModal
        open={isLeadsModalOpen}
        initialView={leadsView}
        onClose={() => setIsLeadsModalOpen(false)}
        onChanged={handleLeadsChanged}
      />
    </div>
  )
}
