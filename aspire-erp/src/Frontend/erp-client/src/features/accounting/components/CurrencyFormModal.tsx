import { useEffect, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import type { Currency } from '../api/useCurrencies'
import { useTranslation } from 'react-i18next'

interface CurrencyFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<Currency>) => Promise<void>
  initialData?: Currency | null
}

export function CurrencyFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
}: CurrencyFormModalProps) {
  const { t } = useTranslation('accounting')
  const [formData, setFormData] = useState<Partial<Currency>>({ isActive: true })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    if (initialData) {
      setFormData(initialData)
    } else {
      setFormData({ isActive: true })
    }
  }, [initialData, isOpen])

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value, type } = e.target
    const checked = (e.target as HTMLInputElement).checked
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)

    if (!formData.code || !formData.symbol) {
      setError(t('currencyForm.errorRequired', 'Code and Symbol are required.'))
      setIsSubmitting(false)
      return
    }

    try {
      // rowVersion/id travel inside formData on edits so the PUT carries the
      // original concurrency token; createCurrency/updateCurrency shape the wire body.
      await onSave({ ...formData })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('currencyForm.errorConflict', 'Concurrency conflict or duplicate code. Please refresh and try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('currencyForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('currencyForm.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  const isEditMode = !!formData.id

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="text-xl">
            {isEditMode ? t('currencyForm.titleEdit', 'Edit Currency') : t('currencyForm.titleNew', 'New Currency')}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4 pt-4">
            {error && <div className="rounded-md bg-red-50 p-3 text-sm text-red-600">{error}</div>}

            <div className="space-y-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('currencyForm.codeLabel', 'Code *')}</label>
                <Input
                  name="code"
                  value={formData.code || ''}
                  onChange={handleChange}
                  placeholder="e.g. USD"
                  maxLength={3}
                  required
                  className="uppercase"
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('currencyForm.symbolLabel', 'Symbol *')}</label>
                <Input
                  name="symbol"
                  value={formData.symbol || ''}
                  onChange={handleChange}
                  placeholder="e.g. $"
                  required
                />
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('currencyForm.fractionLabel', 'Fraction Name')}</label>
              <Input
                name="fractionName"
                value={formData.fractionName || ''}
                onChange={handleChange}
                placeholder="e.g. Cent"
              />
            </div>

            <div className="flex items-center space-x-2 pb-4">
              <input
                type="checkbox"
                name="isActive"
                checked={formData.isActive !== false}
                onChange={handleChange}
                id="isActiveCurrency"
                className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
              />
              <label htmlFor="isActiveCurrency" className="text-sm font-medium">
                {t('currencyForm.active', 'Active')}
              </label>
            </div>
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('currencyForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('currencyForm.saving', 'Saving...') : t('currencyForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
