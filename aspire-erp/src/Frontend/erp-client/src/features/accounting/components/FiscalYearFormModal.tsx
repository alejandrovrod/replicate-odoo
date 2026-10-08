import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useTenantStore } from '../../../store/useTenantStore'
import { createFiscalYear } from '../api/useFiscalYears'
import i18n from '../../../lib/i18n'

interface FiscalYearFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSaved: () => void
}

/**
 * Fiscal-year create modal (R-13 Task 5.1): `YearName + StartDate < EndDate`.
 * Overlap with the same company's years is rejected server-side (`fiscal_year_overlap`)
 * and surfaced through the localized `error` catalog.
 */
export function FiscalYearFormModal({ isOpen, onClose, onSaved }: FiscalYearFormModalProps) {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore((s) => s.companyId)

  const [yearName, setYearName] = useState('')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const reset = () => {
    setYearName('')
    setStartDate('')
    setEndDate('')
    setFormError(null)
  }

  const handleClose = () => {
    reset()
    onClose()
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!companyId) return
    if (!yearName.trim() || !startDate || !endDate) {
      setFormError(t('fiscalYearForm.errorRequired'))
      return
    }
    if (startDate >= endDate) {
      setFormError(t('fiscalYearForm.errorDateOrder'))
      return
    }
    setSaving(true)
    setFormError(null)
    try {
      await createFiscalYear({
        companyId,
        yearName: yearName.trim(),
        startDate,
        endDate,
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
      setSaving(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('fiscalYearForm.titleNew')}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4 pt-4" noValidate>
          <div className="space-y-2">
            <label htmlFor="fy-yearName" className="text-sm font-medium">
              {t('fiscalYearForm.nameLabel')}
            </label>
            <Input
              id="fy-yearName"
              value={yearName}
              placeholder={t('fiscalYearForm.namePlaceholder')}
              onChange={(e) => setYearName(e.target.value)}
              required
              aria-invalid={formError !== null}
              autoComplete="off"
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label htmlFor="fy-startDate" className="text-sm font-medium">
                {t('fiscalYearForm.startLabel')}
              </label>
              <Input
                id="fy-startDate"
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                required
              />
            </div>
            <div className="space-y-2">
              <label htmlFor="fy-endDate" className="text-sm font-medium">
                {t('fiscalYearForm.endLabel')}
              </label>
              <Input
                id="fy-endDate"
                type="date"
                value={endDate}
                min={startDate || undefined}
                onChange={(e) => setEndDate(e.target.value)}
                required
              />
            </div>
          </div>
          {formError && (
            <p role="alert" className="text-sm text-red-600">
              {formError}
            </p>
          )}
          <DialogFooter className="pt-2">
            <Button variant="outline" onClick={handleClose} type="button">
              {t('fiscalYearForm.cancel')}
            </Button>
            <Button type="submit" disabled={saving || !companyId}>
              {saving ? t('fiscalYearForm.saving') : t('fiscalYearForm.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
