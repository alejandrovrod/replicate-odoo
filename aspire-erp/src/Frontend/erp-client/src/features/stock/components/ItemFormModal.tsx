import { useEffect, useMemo, useState } from 'react'
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
import type { Item } from '../types'
import { useTranslation } from 'react-i18next'
import type { AccountTreeNode } from '../../accounting/types'
import { useAccountTree } from '../../accounting/useAccountTree'
import { useCustomFields } from '../../extensibility/api/useCustomFields'
import {
  CustomFieldsRenderer,
  parseCustomData,
  serializeCustomData,
  type CustomFieldValues,
} from '../../../components/forms/CustomFieldsRenderer'

interface ItemFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<Item>) => Promise<void>
  initialData?: Item | null
  companyId: string
}

export function ItemFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
  companyId,
}: ItemFormModalProps) {
  const { t } = useTranslation('stock')
  const { t: tCommon } = useTranslation('common')
  const [formData, setFormData] = useState<Partial<Item>>({
    isActive: true,
    valuationMethod: 'Fifo',
  })
  const [customValues, setCustomValues] = useState<CustomFieldValues>({})
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const customFieldsQuery = useCustomFields(isOpen ? 'Item' : '')

  // Accounts lookup (for Income/Expense default accounts)
  const accountsQuery = useAccountTree(companyId)
  const postingAccounts = useMemo(() => {
    const flat: AccountTreeNode[] = []
    const walk = (nodes: AccountTreeNode[]): void => {
      for (const node of nodes) {
        if (node.isGroup) walk(node.children)
        else flat.push(node)
      }
    }
    walk(accountsQuery.status === 'success' ? accountsQuery.nodes : [])
    return flat.sort((a, b) => a.code.localeCompare(b.code))
  }, [accountsQuery])

  useEffect(() => {
    if (initialData) {
      setFormData(initialData)
      setCustomValues(parseCustomData(initialData.customData))
    } else {
      setFormData({ isActive: true, valuationMethod: 'Fifo' })
      setCustomValues({})
    }
  }, [initialData, companyId, isOpen])

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
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

    if (!formData.name || !formData.code) {
      setError(t('itemForm.errorRequired', 'Name and Code are required.'))
      setIsSubmitting(false)
      return
    }
    if (!formData.baseUOMId) {
      setError(t('itemForm.errorUomRequired', 'A Base UOM is required.'))
      setIsSubmitting(false)
      return
    }

    try {
      // CustomData travels as a JSON object string (extensibility Fase 1); null/empty
      // means "no custom attributes".
      await onSave({
        ...formData,
        incomeAccountId: formData.incomeAccountId || undefined,
        expenseAccountId: formData.expenseAccountId || undefined,
        customData: serializeCustomData(customValues),
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('itemForm.errorConflict', 'Concurrency conflict or duplicate code. Please refresh and try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('itemForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('itemForm.errorGeneric', 'An error occurred while saving.'))
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
            {initialData ? t('itemForm.titleEdit', 'Edit Item') : t('itemForm.titleNew', 'New Item')}
          </DialogTitle>
        </DialogHeader>
        
        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <div className="space-y-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('itemForm.codeLabel', 'Item Code *')}</label>
                <Input
                  name="code"
                  value={formData.code || ''}
                  onChange={handleChange}
                  placeholder={t('itemForm.codePlaceholder', 'e.g. IT-001')}
                  required
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('itemForm.nameLabel', 'Item Name *')}</label>
                <Input
                  name="name"
                  value={formData.name || ''}
                  onChange={handleChange}
                  placeholder={t('itemForm.namePlaceholder', 'e.g. Laptop')}
                  required
                />
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('itemForm.uomLabel', 'Base UOM (GUID) *')}</label>
              <Input
                name="baseUOMId"
                value={formData.baseUOMId || ''}
                onChange={handleChange}
                placeholder={t('itemForm.uomPlaceholder', 'e.g. uuid')}
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('itemForm.valuationLabel', 'Valuation Method')}</label>
              <select
                name="valuationMethod"
                value={formData.valuationMethod || 'Fifo'}
                onChange={handleChange}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200"
              >
                <option value="Fifo">{t('itemForm.valuationFifo', 'FIFO')}</option>
                <option value="MovingAverage">{t('itemForm.valuationMovingAverage', 'Moving Average')}</option>
                <option value="Lifo">{t('itemForm.valuationLifo', 'LIFO')}</option>
              </select>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('itemForm.incomeAccountLabel', 'Income Account')}</label>
              <select
                name="incomeAccountId"
                value={formData.incomeAccountId || ''}
                onChange={handleChange}
                disabled={accountsQuery.status !== 'success'}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
              >
                <option value="">{t('itemForm.accountNone', '-- None --')}</option>
                {postingAccounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('itemForm.expenseAccountLabel', 'Expense/COGS Account')}</label>
              <select
                name="expenseAccountId"
                value={formData.expenseAccountId || ''}
                onChange={handleChange}
                disabled={accountsQuery.status !== 'success'}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
              >
                <option value="">{t('itemForm.accountNone', '-- None --')}</option>
                {postingAccounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex items-center space-x-2 pb-4">
              <input
                type="checkbox"
                name="isActive"
                checked={formData.isActive !== false}
                onChange={handleChange}
                id="isActiveItem"
                className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
              />
              <label htmlFor="isActiveItem" className="text-sm font-medium">
                {t('itemForm.active', 'Active')}
              </label>
            </div>

            {/* Batch-and-serial Fase 3 traceability flags (always sent, like isActive). */}
            <div className="space-y-2 rounded-lg border border-slate-200 bg-slate-50 p-3">
              <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                {t('itemForm.trackingTitle', 'Traceability')}
              </span>
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  name="hasBatchNo"
                  checked={formData.hasBatchNo === true}
                  onChange={handleChange}
                  id="hasBatchNoItem"
                  className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                />
                <label htmlFor="hasBatchNoItem" className="text-sm font-medium">
                  {t('itemForm.hasBatchNo', 'Batch-tracked (lot + expiry, FEFO)')}
                </label>
              </div>
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  name="hasSerialNo"
                  checked={formData.hasSerialNo === true}
                  onChange={handleChange}
                  id="hasSerialNoItem"
                  className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                />
                <label htmlFor="hasSerialNoItem" className="text-sm font-medium">
                  {t('itemForm.hasSerialNo', 'Serial-tracked (one serial per unit)')}
                </label>
              </div>
            </div>

            {customFieldsQuery.definitions.length > 0 && (
              <>
                <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-500 pt-2">
                  {tCommon('customFields.title', 'Custom Fields')}
                </h4>
                <CustomFieldsRenderer
                  definitions={customFieldsQuery.definitions}
                  values={customValues}
                  onChange={(fieldName, value) =>
                    setCustomValues((prev) => ({ ...prev, [fieldName]: value }))
                  }
                />
              </>
            )}
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('itemForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('itemForm.saving', 'Saving...') : t('itemForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
