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
import type { Customer } from '../api/useCustomers'
import { useTranslation } from 'react-i18next'

interface CustomerFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<Customer>) => Promise<void>
  initialData?: Customer | null
  companyId: string
}

export function CustomerFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
  companyId,
}: CustomerFormModalProps) {
  const { t } = useTranslation('selling')
  const [formData, setFormData] = useState<Partial<Customer>>({
    companyId,
    isActive: true,
  })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    if (initialData) {
      setFormData(initialData)
    } else {
      setFormData({ companyId, isActive: true })
    }
  }, [initialData, companyId, isOpen])

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value, type } = e.target
    const checked = (e.target as HTMLInputElement).checked
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : type === 'number' ? parseFloat(value) : value,
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)

    if (!formData.name || !formData.customerCode) {
      setError(t('customerForm.errorRequired', 'Name and Code are required.'))
      setIsSubmitting(false)
      return
    }

    try {
      await onSave({
        ...formData,
        code: formData.customerCode, // Command expects 'code' or controller maps it, but API requires 'code'
      } as any)
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('customerForm.errorConflict', 'Concurrency conflict or duplicate code. Please refresh and try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('customerForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('customerForm.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>
            {initialData ? t('customerForm.titleEdit', 'Edit Customer') : t('customerForm.titleNew', 'New Customer')}
          </DialogTitle>
        </DialogHeader>
        
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && <div className="text-sm text-red-600">{error}</div>}

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('customerForm.codeLabel', 'Customer Code *')}</label>
              <Input
                name="customerCode"
                value={formData.customerCode || ''}
                onChange={handleChange}
                placeholder="e.g. CUST-001"
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('customerForm.nameLabel', 'Customer Name *')}</label>
              <Input
                name="name"
                value={formData.name || ''}
                onChange={handleChange}
                placeholder="e.g. Acme Corp"
                required
              />
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('customerForm.taxIdLabel', 'Tax ID')}</label>
            <Input
              name="taxId"
              value={formData.taxId || ''}
              onChange={handleChange}
              placeholder="Optional Tax ID"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('customerForm.creditLimitLabel', 'Credit Limit')}</label>
            <Input
              type="number"
              name="creditLimit"
              value={formData.creditLimit || ''}
              onChange={handleChange}
              placeholder="0.00"
              step="0.01"
            />
          </div>

          <div className="flex items-center space-x-2">
            <input
              type="checkbox"
              name="isActive"
              checked={formData.isActive !== false}
              onChange={handleChange}
              id="isActiveCust"
              className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
            />
            <label htmlFor="isActiveCust" className="text-sm font-medium">
              {t('customerForm.active', 'Active')}
            </label>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {t('customerForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('customerForm.saving', 'Saving...') : t('customerForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
