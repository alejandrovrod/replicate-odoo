import { Printer, RefreshCw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../components/ui/Dialog'
import { Button } from '../../components/ui/Button'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { useSalarySlipPrintable } from './useHrPayrollData'
import { SalarySlipPrintView } from './SalarySlipPrintView'

interface Props {
  /** Non-null opens the modal and drives which slip is loaded (null closes it). */
  target: { entryId: string; slipId: string } | null
  companyId: string
  onClose: () => void
}

/**
 * Pay-stub viewer (spec: receipt view): loads the printable slip DTO and renders the formal
 * layout with a native "Print / Save as PDF" action. `window.print()` is styled by the
 * `@media print` block in `index.css`, which reveals only the `.salary-slip-printable` subtree -
 * so the browser's own "Save as PDF" produces a clean one-page receipt with no extra tooling.
 */
export function SalarySlipModal({ target, companyId, onClose }: Props) {
  const { t, i18n } = useTranslation('hr-payroll')
  const { data, status, error, reload } = useSalarySlipPrintable(
    companyId,
    target?.entryId ?? null,
    target?.slipId ?? null,
  )

  return (
    <Dialog open={target !== null} onOpenChange={(open) => { if (!open) onClose() }}>
      <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('salarySlip.title')}</DialogTitle>
          <DialogDescription>{t('salarySlip.description')}</DialogDescription>
        </DialogHeader>

        {status === 'loading' ? (
          <p className="py-10 text-center text-sm text-slate-500">{t('salarySlip.loading')}</p>
        ) : null}

        {status === 'error' ? (
          <div className="space-y-3 py-6 text-center">
            <p className="text-sm text-rose-600">
              {translateErrorCode(i18n, error?.code, error?.message) || t('salarySlip.loadError')}
            </p>
            <Button type="button" variant="outline" size="sm" onClick={reload}>
              <RefreshCw className="mr-2 h-4 w-4" />
              {t('salarySlip.retry')}
            </Button>
          </div>
        ) : null}

        {status === 'success' && data ? <SalarySlipPrintView data={data} /> : null}

        <DialogFooter className="salary-slip-no-print gap-2">
          <Button type="button" variant="outline" onClick={onClose}>
            {t('salarySlip.close')}
          </Button>
          <Button
            type="button"
            onClick={() => window.print()}
            disabled={status !== 'success'}
          >
            <Printer className="mr-2 h-4 w-4" />
            {t('salarySlip.print')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
