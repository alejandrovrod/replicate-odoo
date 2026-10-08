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
import { useCurrencies } from '../../accounting/api/useCurrencies'
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
  const currenciesQuery = useCurrencies()

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
      setError(t('supplierForm.errorRequired', 'Name and Code are required.'))
      setIsSubmitting(false)
      return
    }

    try {
      // rowVersion/id travel inside formData on edits so the PUT carries the
      // original concurrency token; createSupplier/updateSupplier shape the wire body.
      await onSave({
        ...formData,
        defaultPayableAccountId: formData.defaultPayableAccountId || undefined,
        currencyId: formData.currencyId || undefined,
      })
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

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <div className="space-y-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('supplierForm.codeLabel', 'Supplier Code *')}</label>
                <Input
                  name="code"
                  value={formData.code || ''}
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

            <div className="space-y-4">
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
                <label className="text-sm font-medium">{t('supplierForm.currencyLabel', 'Currency')}</label>
                <select
                  name="currencyId"
                  value={formData.currencyId || ''}
                  onChange={handleChange}
                  disabled={currenciesQuery.status !== 'success'}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
                >
                  <option value="">{t('supplierForm.currencyNone', '-- Default (USD) --')}</option>
                  {currenciesQuery.status === 'success' && currenciesQuery.items.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.symbol}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('supplierForm.payableAccountLabel', 'Default Payable Account')}</label>
                <select
                  name="defaultPayableAccountId"
                  value={formData.defaultPayableAccountId || ''}
                  onChange={handleChange}
                  disabled={accountsStatus !== 'success'}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
                >
                  <option value="">{t('supplierForm.accountNone', '-- Select Account --')}</option>
                  {payableAccounts.map((account) => (
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
                id="isActiveSupp"
                className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
              />
              <label htmlFor="isActiveSupp" className="text-sm font-medium">
                {t('supplierForm.active', 'Active')}
              </label>
            </div>
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
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
