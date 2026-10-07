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
import type { Supplier } from '../api/useSuppliers'
import { useTranslation } from 'react-i18next'
import { useAccountTree } from '../../accounting/useAccountTree'
import { useTenantStore } from '../../../store/useTenantStore'
import type { AccountTreeNode } from '../../accounting/types'

interface SupplierFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<Supplier>) => Promise<void>
  initialData?: Supplier | null
}

export function SupplierFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
}: SupplierFormModalProps) {
  const { t } = useTranslation('buying')
  const [formData, setFormData] = useState<Partial<Supplier>>({
    isActive: true,
  })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    if (initialData) {
      setFormData(initialData)
    } else {
      setFormData({ isActive: true })
    }
  }, [initialData, isOpen])

  const companyId = useTenantStore((state) => state.companyId)
  const { nodes, status: accountsStatus } = useAccountTree(companyId || '')

  const payableAccounts: { id: string; name: string; code: string }[] = []
  if (accountsStatus === 'success') {
    const walk = (treeNodes: AccountTreeNode[]) => {
      for (const node of treeNodes) {
        if (!node.isGroup && node.type === 'Payable') {
          payableAccounts.push({ id: node.id, name: node.name, code: node.code })
        }
        walk(node.children)
      }
    }
    walk(nodes)
  }

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

    if (!formData.name || !formData.supplierCode) {
      setError(t('supplierForm.errorRequired', 'Name and Code are required.'))
      setIsSubmitting(false)
      return
    }

    try {
      await onSave({
        ...formData,
        code: formData.supplierCode, // Remap if necessary based on API contract
      } as any)
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('supplierForm.errorConflict', 'Concurrency conflict or duplicate code. Please refresh and try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('supplierForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('supplierForm.errorGeneric', 'An error occurred while saving.'))
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
            {initialData ? t('supplierForm.titleEdit', 'Edit Supplier') : t('supplierForm.titleNew', 'New Supplier')}
          </DialogTitle>
        </DialogHeader>
        
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && <div className="text-sm text-red-600">{error}</div>}

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('supplierForm.codeLabel', 'Supplier Code *')}</label>
              <Input
                name="supplierCode"
                value={formData.supplierCode || ''}
                onChange={handleChange}
                placeholder="e.g. SUP-001"
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('supplierForm.nameLabel', 'Supplier Name *')}</label>
              <Input
                name="name"
                value={formData.name || ''}
                onChange={handleChange}
                placeholder="e.g. Acme Corp"
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('supplierForm.taxIdLabel', 'Tax ID')}</label>
              <Input
                name="taxId"
                value={formData.taxId || ''}
                onChange={handleChange}
                placeholder="Optional Tax ID"
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('supplierForm.payableAccountLabel', 'Default Payable Account')}</label>
              <select
                name="payableAccountId"
                value={formData.payableAccountId || ''}
                onChange={handleChange}
                disabled={accountsStatus !== 'success'}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-950 disabled:opacity-50"
              >
                <option value="">{t('supplierForm.accountNone', '-- Select Account --')}</option>
                {payableAccounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="flex items-center space-x-2">
            <input
              type="checkbox"
              name="isActive"
              checked={formData.isActive !== false}
              onChange={handleChange}
              id="isActiveSupp"
              className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
            />
            <label htmlFor="isActiveSupp" className="text-sm font-medium">
              {t('supplierForm.active', 'Active')}
            </label>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {t('supplierForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('supplierForm.saving', 'Saving...') : t('supplierForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
