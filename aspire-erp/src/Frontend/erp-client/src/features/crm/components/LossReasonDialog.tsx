import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import type { CrmCatalogDto } from '../types/crm'

interface Props {
  open: boolean
  reasons: CrmCatalogDto[]
  loadingReasons: boolean
  reasonsError: string | null
  onClose: () => void
  /** Confirmed with a catalog reason plus optional free-text notes. */
  onConfirm: (lossReasonId: string, notes: string) => void
}

/**
 * ClosedLost confirmation (replaces window.prompt): a Radix dialog (focus trap, Escape
 * to cancel, labelled controls - WCAG 2.1 AA) with the normalized loss-reason catalog
 * plus an observations field. The confirm button stays disabled until a reason is
 * picked, so the CRM-02 invariant can never be skipped at the UI level.
 */
export function LossReasonDialog({
  open,
  reasons,
  loadingReasons,
  reasonsError,
  onClose,
  onConfirm,
}: Props) {
  const { t } = useTranslation('crm')
  const [reasonId, setReasonId] = useState('')
  const [notes, setNotes] = useState('')

  useEffect(() => {
    if (open) {
      setReasonId('')
      setNotes('')
    }
  }, [open ])

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-w-md" aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>{t('lossReason.title', 'Close as Lost')}</DialogTitle>
          <DialogDescription>
            {t('lossReason.description', 'Pick the normalized loss reason. Notes are optional.')}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div className="space-y-1">
            <label htmlFor="loss-reason-select" className="text-sm font-medium text-slate-800">
              {t('lossReason.label', 'Loss Reason')}
            </label>
            {loadingReasons ? (
              <p className="text-xs text-slate-500">{t('lossReason.loading', 'Loading reasons…')}</p>
            ) : reasonsError ? (
              <p className="text-xs text-red-600">{reasonsError}</p>
            ) : (
              <select
                id="loss-reason-select"
                value={reasonId}
                onChange={(e) => setReasonId(e.target.value)}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                required
              >
                <option value="">{t('lossReason.placeholder', '-- Select a reason --')}</option>
                {reasons.map((reason) => (
                  <option key={reason.id} value={reason.id}>
                    {reason.name}
                  </option>
                ))}
              </select>
            )}
          </div>

          <div className="space-y-1">
            <label htmlFor="loss-reason-notes" className="text-sm font-medium text-slate-800">
              {t('lossReason.notesLabel', 'Observations')}
            </label>
            <textarea
              id="loss-reason-notes"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={3}
              placeholder={t('lossReason.notesPlaceholder', 'Additional context about the loss…')}
              className="flex w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
            />
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {t('salesOrder.cancel', 'Cancel')}
          </Button>
          <Button
            type="button"
            disabled={!reasonId}
            onClick={() => onConfirm(reasonId, notes.trim())}
          >
            {t('lossReason.confirm', 'Close as Lost')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
