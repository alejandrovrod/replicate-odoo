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
  const [formData, setFormData] = useState<Partial<Item>>({
    companyId,
    isActive: true,
    valuationMethod: 'FIFO',
  })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

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
    } else {
      setFormData({ companyId, isActive: true, valuationMethod: 'FIFO' })
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
      await onSave({
        ...formData,
        incomeAccountId: formData.incomeAccountId || undefined,
        expenseAccountId: formData.expenseAccountId || undefined,
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
        
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && <div className="text-sm text-red-600">{error}</div>}

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('itemForm.codeLabel', 'Item Code *')}</label>
              <Input
                name="code"
                value={formData.code || ''}
                onChange={handleChange}
                placeholder="e.g. IT-001"
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('itemForm.nameLabel', 'Item Name *')}</label>
              <Input
                name="name"
                value={formData.name || ''}
                onChange={handleChange}
                placeholder="e.g. Laptop"
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
              placeholder="e.g. uuid"
              required
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('itemForm.valuationLabel', 'Valuation Method')}</label>
            <select
              name="valuationMethod"
              value={formData.valuationMethod || 'FIFO'}
              onChange={handleChange}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-950"
            >
              <option value="FIFO">FIFO</option>
              <option value="MovingAverage">Moving Average</option>
            </select>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('itemForm.incomeAccountLabel', 'Income Account')}</label>
            <select
              name="incomeAccountId"
              value={formData.incomeAccountId || ''}
              onChange={handleChange}
              disabled={accountsQuery.status !== 'success'}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-950 disabled:opacity-50"
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
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-950 disabled:opacity-50"
            >
              <option value="">{t('itemForm.accountNone', '-- None --')}</option>
              {postingAccounts.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.code} — {account.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex items-center space-x-2">
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

          <DialogFooter>
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
