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
import type { Warehouse } from '../api/useWarehouses'
import { useTranslation } from 'react-i18next'
import type { AccountTreeNode } from '../../accounting/types'
import { useAccountTree } from '../../accounting/useAccountTree'

interface WarehouseFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<Warehouse>) => Promise<void>
  initialData?: Warehouse | null
  companyId: string
  groupWarehouses: Warehouse[]
}

export function WarehouseFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
  companyId,
  groupWarehouses,
}: WarehouseFormModalProps) {
  const { t } = useTranslation('stock')
  const [formData, setFormData] = useState<Partial<Warehouse>>({
    companyId,
    isActive: true,
    isGroup: false,
  })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Stock-account picker: only leaf posting accounts can hold inventory (groups never post).
  // The tree endpoint is exempt from pagination by design, so one read is complete.
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
      setFormData({ companyId, isActive: true, isGroup: false })
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

    // Basic validation
    if (!formData.name || !formData.code) {
      setError(t('warehouseForm.errorRequired', 'Name and Code are required.'))
      setIsSubmitting(false)
      return
    }
    if (!formData.accountId) {
      setError(t('warehouseForm.errorAccountRequired', 'A stock account is required.'))
      setIsSubmitting(false)
      return
    }

    try {
      await onSave({
        ...formData,
        parentWarehouseId: formData.parentWarehouseId || undefined, // convert empty to undefined
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('warehouseForm.errorConflict', 'Concurrency conflict or duplicate code. Please refresh and try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('warehouseForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('warehouseForm.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {initialData ? t('warehouseForm.titleEdit', 'Edit Warehouse') : t('warehouseForm.titleNew', 'New Warehouse')}
          </DialogTitle>
        </DialogHeader>
        
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && <div className="text-sm text-red-600">{error}</div>}

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('warehouseForm.codeLabel', 'Warehouse Code *')}</label>
            <Input
              name="code"
              value={formData.code || ''}
              onChange={handleChange}
              placeholder={t('warehouseForm.codePlaceholder', 'e.g. WH-001')}
              required
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('warehouseForm.nameLabel', 'Warehouse Name *')}</label>
            <Input
              name="name"
              value={formData.name || ''}
              onChange={handleChange}
              placeholder={t('warehouseForm.namePlaceholder', 'e.g. Main Warehouse')}
              required
            />
          </div>

          <div className="flex items-center space-x-2">
            <input
              type="checkbox"
              name="isGroup"
              checked={formData.isGroup || false}
              onChange={handleChange}
              id="isGroup"
              className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
            />
            <label htmlFor="isGroup" className="text-sm font-medium">
              {t('warehouseForm.isGroup', 'Is Group Warehouse')}
            </label>
          </div>

          <div className="flex items-center space-x-2">
            <input
              type="checkbox"
              name="isActive"
              checked={formData.isActive !== false}
              onChange={handleChange}
              id="isActive"
              className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
            />
            <label htmlFor="isActive" className="text-sm font-medium">
              {t('warehouseForm.active', 'Active')}
            </label>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('warehouseForm.parentLabel', 'Parent Warehouse')}</label>
            <select
              name="parentWarehouseId"
              value={formData.parentWarehouseId || ''}
              onChange={handleChange}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200"
            >
              <option value="">{t('warehouseForm.parentNone', '-- None --')}</option>
              {groupWarehouses.map((wh) => (
                <option key={wh.id} value={wh.id}>
                  {wh.name}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('warehouseForm.accountLabel', 'Account ID')}</label>
            <select
              name="accountId"
              value={formData.accountId || ''}
              onChange={handleChange}
              disabled={accountsQuery.status !== 'success'}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
            >
              <option value="">
                {accountsQuery.status !== 'success'
                  ? t('warehouseForm.accountLoading', 'Loading accounts…')
                  : t('warehouseForm.accountPlaceholder', 'Select a stock account…')}
              </option>
              {postingAccounts.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.code} — {account.name}
                </option>
              ))}
            </select>
            <p className="text-xs text-slate-500">{t('warehouseForm.accountHint', 'Must be a valid stock account GUID.')}</p>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {t('warehouseForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('warehouseForm.saving', 'Saving...') : t('warehouseForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
