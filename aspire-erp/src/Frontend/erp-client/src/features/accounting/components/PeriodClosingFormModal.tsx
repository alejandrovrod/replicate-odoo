import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useTenantStore } from '../../../store/useTenantStore'
import { useAccountTree } from '../useAccountTree'
import { useFiscalYears } from '../api/useFiscalYears'
import {
  createClosingVoucher,
  useClosingPreview,
  type ClosingPreview,
} from '../api/usePeriodClosing'
import i18n from '../../../lib/i18n'

interface PeriodClosingFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSaved: () => void
}

const formatAmount = (value: number) =>
  value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

/**
 * Closing-voucher create wizard (R-13 Task 5.2): picks one OPEN fiscal year, a posting
 * date inside it, and the retained-earnings leaf (empty = company default, FC-03).
 * The read-only P&L preview below comes from `GET …/unclosed-balances` — the same
 * balance query submit will run — with the net banner crediting/debiting retained.
 */
export function PeriodClosingFormModal({ isOpen, onClose, onSaved }: PeriodClosingFormModalProps) {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore((s) => s.companyId)
  const { nodes, status: accountsStatus } = useAccountTree(companyId || '')

  const { items: openYears } = useFiscalYears({ isClosed: false, page: 1, pageSize: 500 })

  const [fiscalYearId, setFiscalYearId] = useState('')
  const [postingDate, setPostingDate] = useState('')
  const [retainedEarningsAccountId, setRetainedEarningsAccountId] = useState('')
  const [remarks, setRemarks] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const selectedYear = useMemo(
    () => openYears.find((y) => y.id === fiscalYearId) ?? null,
    [openYears, fiscalYearId],
  )

  const { preview, status: previewStatus } = useClosingPreview(isOpen ? fiscalYearId || null : null)

  const equityAccounts = useMemo(() => {
    const out: { id: string; code: string; name: string }[] = []
    if (accountsStatus !== 'success') return out
    const walk = (treeNodes: typeof nodes) => {
      for (const node of treeNodes) {
        if (!node.isGroup && node.isActive && node.rootType === 'Equity') {
          out.push({ id: node.id, code: node.code, name: node.name })
        }
        if (node.children) walk(node.children)
      }
    }
    walk(nodes)
    return out.sort((a, b) => a.code.localeCompare(b.code))
  }, [accountsStatus, nodes])

  const reset = () => {
    setFiscalYearId('')
    setPostingDate('')
    setRetainedEarningsAccountId('')
    setRemarks('')
    setFormError(null)
  }

  const handleClose = () => {
    if (submitting) return
    reset()
    onClose()
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!companyId || submitting) return
    if (!fiscalYearId || !postingDate) {
      setFormError(t('periodClosing.formErrorRequired'))
      return
    }
    if (selectedYear && (postingDate < selectedYear.startDate || postingDate > selectedYear.endDate)) {
      setFormError(t('periodClosing.formErrorDateOutside'))
      return
    }
    setSubmitting(true)
    setFormError(null)
    try {
      await createClosingVoucher({
        companyId,
        fiscalYearId,
        postingDate,
        retainedEarningsAccountId: retainedEarningsAccountId || undefined,
        remarks: remarks || undefined,
      })
      reset()
      onSaved()
      onClose()
    } catch (cause: unknown) {
      const apiError = cause instanceof ApiError ? cause : null
      setFormError(
        translateErrorCode(i18n, apiError?.code, apiError?.message ?? t('fiscalYearForm.errorGeneric')),
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('periodClosing.newTitle')}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col overflow-hidden" noValidate>
          <div className="flex-1 space-y-4 overflow-y-auto py-4 pr-1">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label htmlFor="pcv-fiscalYear" className="text-sm font-medium">
                  {t('periodClosing.formYearLabel')}
                </label>
                <select
                  id="pcv-fiscalYear"
                  required
                  value={fiscalYearId}
                  onChange={(e) => setFiscalYearId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 text-sm"
                >
                  <option value="">{t('periodClosing.formYearPlaceholder')}</option>
                  {openYears.map((y) => (
                    <option key={y.id} value={y.id}>
                      {y.yearName} ({y.startDate} → {y.endDate})
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label htmlFor="pcv-postingDate" className="text-sm font-medium">
                  {t('periodClosing.formPostingDateLabel')}
                </label>
                <Input
                  id="pcv-postingDate"
                  type="date"
                  required
                  value={postingDate}
                  min={selectedYear?.startDate || undefined}
                  max={selectedYear?.endDate || undefined}
                  onChange={(e) => setPostingDate(e.target.value)}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label htmlFor="pcv-retained" className="text-sm font-medium">
                  {t('periodClosing.formRetainedLabel')}
                </label>
                <select
                  id="pcv-retained"
                  value={retainedEarningsAccountId}
                  onChange={(e) => setRetainedEarningsAccountId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 text-sm"
                >
                  <option value="">{t('periodClosing.formRetainedDefault')}</option>
                  {equityAccounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.code} - {a.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label htmlFor="pcv-remarks" className="text-sm font-medium">
                  {t('periodClosing.formRemarksLabel')}
                </label>
                <Input
                  id="pcv-remarks"
                  value={remarks}
                  onChange={(e) => setRemarks(e.target.value)}
                  autoComplete="off"
                />
              </div>
            </div>

            {fiscalYearId && (
              <PreviewPanel preview={preview} status={previewStatus} />
            )}

            {formError && (
              <p role="alert" className="text-sm text-red-600">
                {formError}
              </p>
            )}
          </div>
          <DialogFooter className="pt-4">
            <Button variant="outline" onClick={handleClose} type="button">
              {t('fiscalYearForm.cancel')}
            </Button>
            <Button type="submit" disabled={submitting || !companyId}>
              {submitting ? t('fiscalYearForm.saving') : t('fiscalYearForm.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function PreviewPanel({
  preview,
  status,
}: {
  preview: ClosingPreview | null
  status: 'idle' | 'loading' | 'success' | 'error'
}) {
  const { t } = useTranslation('accounting')
  if (status === 'loading' || status === 'idle') {
    return <p className="text-sm text-slate-500">{t('periodClosing.previewLoading')}</p>
  }
  if (status === 'error' || !preview) return null
  if (preview.lines.length === 0) {
    return (
      <p role="status" className="rounded-md bg-amber-50 p-3 text-sm text-amber-800">
        {t('periodClosing.previewEmpty')}
      </p>
    )
  }
  const netLabel =
    preview.net > 0
      ? t('periodClosing.previewNetProfit', { amount: formatAmount(preview.net) })
      : preview.net < 0
        ? t('periodClosing.previewNetLoss', { amount: formatAmount(Math.abs(preview.net)) })
        : t('periodClosing.previewNetZero')
  return (
    <section aria-label={t('periodClosing.previewTitle')} className="space-y-2">
      <h3 className="text-sm font-semibold text-slate-900">{t('periodClosing.previewTitle')}</h3>
      <div className="overflow-hidden rounded-md border border-slate-200">
        <table className="w-full text-left text-xs text-slate-600">
          <thead className="bg-slate-50 text-slate-900">
            <tr>
              <th scope="col" className="px-4 py-2 font-medium">
                {t('periodClosing.previewColAccount')}
              </th>
              <th scope="col" className="px-4 py-2 text-right font-medium">
                {t('periodClosing.previewColDebit')}
              </th>
              <th scope="col" className="px-4 py-2 text-right font-medium">
                {t('periodClosing.previewColCredit')}
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {preview.lines.map((line) => (
              <tr key={line.accountId}>
                <td className="px-4 py-2">
                  {line.code} - {line.name}
                </td>
                <td className="px-4 py-2 text-right tabular-nums">{formatAmount(line.debit)}</td>
                <td className="px-4 py-2 text-right tabular-nums">{formatAmount(line.credit)}</td>
              </tr>
            ))}
            {preview.retainedLine && (
              <tr className="bg-sky-50 font-medium">
                <td className="px-4 py-2">
                  {preview.retainedLine.code} - {preview.retainedLine.name}{' '}
                  <span className="ml-1 rounded-full bg-sky-100 px-2 py-0.5 text-[10px] text-sky-800">
                    {t('periodClosing.previewRetainedBadge')}
                  </span>
                </td>
                <td className="px-4 py-2 text-right tabular-nums">
                  {formatAmount(preview.retainedLine.debit)}
                </td>
                <td className="px-4 py-2 text-right tabular-nums">
                  {formatAmount(preview.retainedLine.credit)}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p role="status" className="text-sm font-medium text-slate-800">
        {netLabel}
      </p>
    </section>
  )
}
