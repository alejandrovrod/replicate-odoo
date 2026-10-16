import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'

/**
 * Splits pasted/scanned input into serial candidates: commas, semicolons, whitespace and
 * newlines all separate; blanks dropped; order preserved; duplicates collapsed (reported).
 */
export function parseSerialInput(raw: string): { serials: string[]; duplicates: string[] } {
  const parts = raw
    .split(/[\s,;]+/)
    .map((s) => s.trim())
    .filter((s) => s.length > 0)
  const seen = new Set<string>()
  const serials: string[] = []
  const duplicates: string[] = []
  for (const part of parts) {
    if (seen.has(part)) {
      if (!duplicates.includes(part)) duplicates.push(part)
      continue
    }
    seen.add(part)
    serials.push(part)
  }
  return { serials, duplicates }
}

interface SerialNoPickerModalProps {
  isOpen: boolean
  onClose: () => void
  /** Item label shown in the title (code — name). */
  itemLabel: string
  /** Units the voucher line moves: the serial count must equal this exactly. */
  expectedCount: number
  initial: string[]
  onApply: (serials: string[]) => void
}

/**
 * Serial capture modal (batch-and-serial Fase 3, gap D5): paste or scan serials (one per
 * line, commas/whitespace also split), live count validation against the line quantity,
 * duplicate detection. The server re-validates warehouse/state authoritatively.
 */
export function SerialNoPickerModal({
  isOpen,
  onClose,
  itemLabel,
  expectedCount,
  initial,
  onApply,
}: SerialNoPickerModalProps) {
  const { t } = useTranslation('stock')
  const [raw, setRaw] = useState('')

  useEffect(() => {
    if (isOpen) setRaw(initial.join('\n'))
  }, [isOpen, initial])

  const { serials, duplicates } = useMemo(() => parseSerialInput(raw), [raw])
  const countOk = serials.length === expectedCount

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>
            {t('tracking.serialTitle', 'Serial numbers — {{item}}', { item: itemLabel })}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-3">
          <p className="text-xs text-slate-500">
            {t(
              'tracking.serialHint',
              'Paste or scan one serial per line. The count must equal the line quantity ({{count}}).',
              { count: expectedCount },
            )}
          </p>
          <textarea
            value={raw}
            onChange={(e) => setRaw(e.target.value)}
            rows={6}
            placeholder="SN-1001&#10;SN-1002"
            aria-label={t('tracking.serialInput', 'Serial numbers')}
            className="w-full rounded-md border border-slate-200 bg-white px-3 py-2 font-mono text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200"
          />
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span
              className={`inline-flex items-center rounded-full px-3 py-1 font-semibold ${
                countOk ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
              }`}
            >
              {t('tracking.serialCount', '{{found}} of {{expected}} serials', {
                found: serials.length,
                expected: expectedCount,
              })}
            </span>
            {duplicates.length > 0 && (
              <span className="inline-flex items-center rounded-full bg-rose-100 px-3 py-1 font-semibold text-rose-800">
                {t('tracking.serialDuplicates', 'duplicated: {{list}}', { list: duplicates.join(', ') })}
              </span>
            )}
          </div>
          {serials.length > 0 && (
            <ul className="max-h-32 space-y-1 overflow-y-auto rounded-lg border border-slate-100 bg-slate-50 p-2 font-mono text-xs text-slate-700">
              {serials.map((serial) => (
                <li key={serial}>{serial}</li>
              ))}
            </ul>
          )}
        </div>

        <DialogFooter className="pt-4 border-t border-slate-100">
          <Button type="button" variant="outline" onClick={onClose}>
            {t('tracking.serialCancel', 'Cancel')}
          </Button>
          <Button
            type="button"
            disabled={!countOk || duplicates.length > 0}
            onClick={() => {
              onApply(serials)
              onClose()
            }}
          >
            {t('tracking.serialApply', 'Apply {{count}} serials', { count: serials.length })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
