import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { Pagination } from '../../../components/ui/Pagination'
import { ApiError } from '../../../api/client'
import { useErpAction } from '../../../lib/useErpAction'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { crmApi } from '../api/crmApi'
import { ConvertLeadDialog } from './ConvertLeadDialog'
import type { ConvertLeadResult, IngestLeadResult, LeadDto } from '../types/crm'

const PAGE_SIZE = 20

/** Default intake source for a manually created lead (stored as data, not UI copy). */
const DEFAULT_LEAD_SOURCE = 'Manual / Ventas'

type View = 'list' | 'form'

/** Status badge palette keyed by the backend `LeadStatus` contract values. */
const STATUS_STYLE: Record<string, string> = {
  Open: 'bg-sky-100 text-sky-700',
  Contacted: 'bg-amber-100 text-amber-700',
  Qualified: 'bg-violet-100 text-violet-700',
  Converted: 'bg-emerald-100 text-emerald-700',
  Lost: 'bg-rose-100 text-rose-700',
}

type StatusKey =
  | 'leads.statusOpen'
  | 'leads.statusContacted'
  | 'leads.statusQualified'
  | 'leads.statusConverted'
  | 'leads.statusLost'

const STATUS_LABEL_KEY: Record<string, StatusKey> = {
  Open: 'leads.statusOpen',
  Contacted: 'leads.statusContacted',
  Qualified: 'leads.statusQualified',
  Converted: 'leads.statusConverted',
  Lost: 'leads.statusLost',
}

interface Props {
  open: boolean
  /** Which view to show on open (the overview's "+ New Lead" jumps straight to the form). */
  initialView?: View
  onClose: () => void
  /** Fired after any successful intake/conversion so the overview refreshes count + board. */
  onChanged: () => void
}

/**
 * Leads manager (spec CRM-04 intake + CRM-03 conversion). Two views inside one accessible
 * dialog: the paginated lead list with per-row conversion, and the quick manual-intake form.
 * The list is the live `GET /api/v1/leads` feed; a new prospect rides `POST /api/v1/leads/ingest`
 * and a conversion rides `POST /api/v1/leads/{id}/convert`, both surfacing RFC 7807 rejections.
 */
export function LeadsManagementModal({ open, initialView = 'list', onClose, onChanged }: Props) {
  const { t } = useTranslation('crm')
  const [view, setView] = useState<View>(initialView)
  const [page, setPage] = useState(1)
  const [leads, setLeads] = useState<LeadDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<unknown>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [convertTarget, setConvertTarget] = useState<LeadDto | null>(null)

  // Stale-response guard: only the newest in-flight page read may commit its rows.
  const requestRef = useRef(0)
  const loadLeads = useCallback((targetPage: number) => {
    const requestId = ++requestRef.current
    setLoading(true)
    crmApi.getLeads(targetPage, PAGE_SIZE).then(
      (result) => {
        if (requestRef.current !== requestId) return
        setLeads(result.items)
        setTotalCount(result.totalCount)
        setLoadError(null)
        setLoading(false)
      },
      (cause: unknown) => {
        if (requestRef.current !== requestId) return
        setLoadError(cause)
        setLoading(false)
      },
    )
  }, [])

  // Seed the requested view on open; reset paging on close so the next open starts at page 1.
  useEffect(() => {
    if (open) {
      setView(initialView)
      setNotice(null)
    } else {
      setPage(1)
    }
  }, [open, initialView])

  // Fetch the visible page (once on open, then on every page change).
  useEffect(() => {
    if (!open) return
    loadLeads(page)
  }, [open, page, loadLeads])

  /** Reloads the first page after an ingest/convert, whether or not we were already there. */
  const reloadFirstPage = () => {
    if (page === 1) loadLeads(1)
    else setPage(1)
  }

  const handleCreated = (result: IngestLeadResult) => {
    setView('list')
    setNotice(
      result.duplicate
        ? t('leads.duplicate', { leadCode: result.lead.leadCode })
        : t('leads.created', { leadCode: result.lead.leadCode }),
    )
    reloadFirstPage()
    onChanged()
  }

  const handleConverted = (result: ConvertLeadResult) => {
    setConvertTarget(null)
    setNotice(
      t('leads.successConvert', {
        opportunityNumber: result.opportunityNumber,
        customerCode: result.customerCode,
      }),
    )
    reloadFirstPage()
    onChanged()
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-w-4xl" aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>{t('leads.title', 'Leads')}</DialogTitle>
        </DialogHeader>

        {view === 'form' ? (
          <LeadIntakeForm onCancel={() => setView('list')} onCreated={handleCreated} />
        ) : (
          <>
            <div className="flex items-center justify-between gap-2">
              <p className="text-xs text-slate-500">
                {t('leads.listSubtitle', 'Prospects awaiting qualification.')}
              </p>
              <Button
                size="sm"
                onClick={() => {
                  setNotice(null)
                  setView('form')
                }}
              >
                + {t('leads.newLead', 'New Lead')}
              </Button>
            </div>

            {notice && (
              <p
                role="status"
                className="rounded-md border border-emerald-200 bg-emerald-50 p-2 text-xs text-emerald-700"
              >
                {notice}
              </p>
            )}

            <div className="min-h-0 flex-1 overflow-auto rounded-lg border border-slate-200">
              {loading ? (
                <p className="p-4 text-xs text-slate-500">{t('leads.loading', 'Loading leads…')}</p>
              ) : loadError ? (
                <p className="p-4 text-xs text-red-600">
                  {loadError instanceof ApiError
                    ? loadError.message
                    : t('leads.loadFailed', 'Failed to load leads.')}
                </p>
              ) : leads.length === 0 ? (
                <p className="p-4 text-xs text-slate-500">
                  {t('leads.empty', 'No leads yet. Create the first one.')}
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('leads.colCode', 'Code')}</TableHead>
                      <TableHead>{t('leads.colName', 'Name / Contact')}</TableHead>
                      <TableHead>{t('leads.colOrganization', 'Company')}</TableHead>
                      <TableHead>{t('leads.colContact', 'Email / Phone')}</TableHead>
                      <TableHead>{t('leads.colSource', 'Source')}</TableHead>
                      <TableHead>{t('leads.colStatus', 'Status')}</TableHead>
                      <TableHead>{t('leads.colActions', 'Actions')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {leads.map((lead) => {
                      const statusKey = STATUS_LABEL_KEY[lead.status]
                      return (
                        <TableRow key={lead.id}>
                          <TableCell className="font-mono text-xs text-slate-700">
                            {lead.leadCode}
                          </TableCell>
                          <TableCell className="text-sm font-medium text-slate-900">
                            {lead.leadName}
                          </TableCell>
                          <TableCell className="text-sm text-slate-600">
                            {lead.organizationName ?? '—'}
                          </TableCell>
                          <TableCell className="text-xs text-slate-600">
                            {lead.email ?? '—'}
                            {lead.phone ? (
                              <span className="block text-slate-400">{lead.phone}</span>
                            ) : null}
                          </TableCell>
                          <TableCell className="text-sm text-slate-600">{lead.source}</TableCell>
                          <TableCell>
                            <span
                              className={`inline-flex rounded-full px-2 py-0.5 text-[11px] font-medium ${
                                STATUS_STYLE[lead.status] ?? 'bg-slate-100 text-slate-600'
                              }`}
                            >
                              {statusKey ? t(statusKey, lead.status) : lead.status}
                            </span>
                          </TableCell>
                          <TableCell>
                            {lead.status === 'Open' ? (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => setConvertTarget(lead)}
                              >
                                {t('leads.convert', 'Convert to Opportunity')}
                              </Button>
                            ) : (
                              <span className="text-xs text-slate-400">—</span>
                            )}
                          </TableCell>
                        </TableRow>
                      )
                    })}
                  </TableBody>
                </Table>
              )}
            </div>

            <Pagination
              totalCount={totalCount}
              page={page}
              pageSize={PAGE_SIZE}
              onPageChange={setPage}
            />

            <DialogFooter className="border-t border-slate-100 pt-4">
              <Button type="button" variant="outline" onClick={onClose}>
                {t('leads.close', 'Close')}
              </Button>
            </DialogFooter>
          </>
        )}

        <ConvertLeadDialog
          lead={convertTarget}
          onClose={() => setConvertTarget(null)}
          onConverted={handleConverted}
        />
      </DialogContent>
    </Dialog>
  )
}

interface IntakeFormProps {
  onCancel: () => void
  onCreated: (result: IngestLeadResult) => void
}

interface IntakeDraft {
  leadName: string
  organizationName: string
  email: string
  phone: string
  source: string
}

/** Client-side lead code: manual intake has no external id, but the route requires one. */
function generateLeadCode(): string {
  const stamp = Date.now().toString(36).toUpperCase()
  const suffix = Math.random().toString(36).slice(2, 6).toUpperCase()
  return `LEAD-${stamp}-${suffix}`
}

/**
 * Quick manual-intake form (View 2). Only the lead name is required; every blank optional
 * field is sent as null so the backend stores an explicit absence. On success the parent
 * returns to the list, refreshes it and bumps the overview counter.
 */
function LeadIntakeForm({ onCancel, onCreated }: IntakeFormProps) {
  const { t, i18n } = useTranslation('crm')
  const [draft, setDraft] = useState<IntakeDraft>({
    leadName: '',
    organizationName: '',
    email: '',
    phone: '',
    source: DEFAULT_LEAD_SOURCE,
  })

  const { state, dispatch, isPending } = useErpAction<IngestLeadResult, IntakeDraft>(async (payload) => {
    const result = await crmApi.ingestLead({
      leadCode: generateLeadCode(),
      leadName: payload.leadName.trim(),
      organizationName: payload.organizationName.trim() || null,
      email: payload.email.trim() || null,
      phone: payload.phone.trim() || null,
      source: payload.source.trim() || DEFAULT_LEAD_SOURCE,
    })
    onCreated(result)
    return result
  })

  const update = (patch: Partial<IntakeDraft>) => setDraft((prev) => ({ ...prev, ...patch }))
  const nameMissing = draft.leadName.trim().length === 0

  return (
    <div className="space-y-3">
      <p className="text-xs text-slate-500">
        {t('leads.formSubtitle', 'Quick manual intake. Only the lead name is required.')}
      </p>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="block text-xs font-medium text-slate-600 sm:col-span-2">
          {t('leads.nameLabel', 'Lead name')}
          <Input
            value={draft.leadName}
            onChange={(e) => update({ leadName: e.target.value })}
            disabled={isPending}
            aria-required="true"
            className="mt-1"
          />
        </label>
        <label className="block text-xs font-medium text-slate-600">
          {t('leads.organizationLabel', 'Company (optional)')}
          <Input
            value={draft.organizationName}
            onChange={(e) => update({ organizationName: e.target.value })}
            disabled={isPending}
            className="mt-1"
          />
        </label>
        <label className="block text-xs font-medium text-slate-600">
          {t('leads.sourceLabel', 'Source')}
          <Input
            value={draft.source}
            onChange={(e) => update({ source: e.target.value })}
            disabled={isPending}
            className="mt-1"
          />
        </label>
        <label className="block text-xs font-medium text-slate-600">
          {t('leads.emailLabel', 'Email (optional)')}
          <Input
            type="email"
            value={draft.email}
            onChange={(e) => update({ email: e.target.value })}
            disabled={isPending}
            className="mt-1"
          />
        </label>
        <label className="block text-xs font-medium text-slate-600">
          {t('leads.phoneLabel', 'Phone (optional)')}
          <Input
            value={draft.phone}
            onChange={(e) => update({ phone: e.target.value })}
            disabled={isPending}
            className="mt-1"
          />
        </label>
      </div>

      {nameMissing && (
        <p className="text-xs text-amber-600">{t('leads.nameRequired', 'Lead name is required.')}</p>
      )}
      {state.error && (
        <p className="text-xs text-red-600">{translateErrorCode(i18n, state.errorCode, state.error)}</p>
      )}

      <DialogFooter className="border-t border-slate-100 pt-4">
        <Button type="button" variant="outline" onClick={onCancel} disabled={isPending}>
          {t('leads.cancel', 'Cancel')}
        </Button>
        <Button type="button" onClick={() => dispatch(draft)} disabled={nameMissing || isPending}>
          {isPending ? t('leads.creating', 'Creating…') : t('leads.create', 'Create Lead')}
        </Button>
      </DialogFooter>
    </div>
  )
}
