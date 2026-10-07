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
import { useTranslation } from 'react-i18next'
import type { AccountTreeNode, AccountType } from '../types'

export interface AccountFormData {
  id?: string
  companyId: string
  accountCode: string
  accountName: string
  rootType: 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense'
  type?: AccountType
  isGroup: boolean
  isActive: boolean
  parentAccountId?: string
  currency?: string
  rowVersion?: string // For edits
}

interface AccountFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: AccountFormData) => Promise<void>
  initialData?: Partial<AccountFormData> | null
  companyId: string
  accountsQuery: { nodes: AccountTreeNode[]; status: string }
}

export function AccountFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
  companyId,
  accountsQuery,
}: AccountFormModalProps) {
  const { t } = useTranslation('accounting')
  
  const [formData, setFormData] = useState<Partial<AccountFormData>>({
    companyId,
    isActive: true,
    isGroup: false,
    currency: 'USD',
    rootType: 'Asset',
    type: 'Other',
  })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Flatten tree to get group accounts only for Parent selection
  const groupAccounts: { id: string; name: string; code: string }[] = []
  if (accountsQuery.status === 'success') {
    const walk = (nodes: AccountTreeNode[]) => {
      for (const node of nodes) {
        if (node.isGroup) {
          groupAccounts.push({ id: node.id, name: node.name, code: node.code })
          walk(node.children)
        }
      }
    }
    walk(accountsQuery.nodes)
  }

  useEffect(() => {
    if (initialData) {
      setFormData(initialData)
    } else {
      setFormData({
        companyId,
        isActive: true,
        isGroup: false,
        currency: 'USD',
        rootType: 'Asset',
        type: 'Other',
      })
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

    if (!formData.accountCode || !formData.accountName) {
      setError(t('accountForm.errorRequired', 'Code and Name are required.'))
      setIsSubmitting(false)
      return
    }

    try {
      await onSave({
        companyId: formData.companyId!,
        accountCode: formData.accountCode!,
        accountName: formData.accountName!,
        rootType: formData.rootType as any,
        type: formData.isGroup ? 'Other' : (formData.type || 'Other'),
        isGroup: formData.isGroup || false,
        isActive: formData.isActive !== false,
        parentAccountId: formData.parentAccountId || undefined,
        currency: formData.currency || 'USD',
        id: formData.id,
        rowVersion: formData.rowVersion,
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('accountForm.errorConflict', 'Concurrency conflict or duplicate code. Please try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('accountForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('accountForm.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  const isEditMode = !!formData.id

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="text-xl">
            {isEditMode ? t('accountForm.titleEdit', 'Edit Account') : t('accountForm.titleNew', 'New Account')}
          </DialogTitle>
        </DialogHeader>
        
        <form onSubmit={handleSubmit} className="space-y-6 pt-4">
          {error && <div className="rounded-md bg-red-50 p-3 text-sm text-red-600">{error}</div>}

          <div className="space-y-6">
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('accountForm.codeLabel', 'Account Code *')}</label>
              <Input
                name="accountCode"
                value={formData.accountCode || ''}
                onChange={handleChange}
                placeholder="e.g. 1100"
                required
                disabled={isEditMode} // Cannot change code after creation in this implementation
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('accountForm.nameLabel', 'Account Name *')}</label>
              <Input
                name="accountName"
                value={formData.accountName || ''}
                onChange={handleChange}
                placeholder="e.g. Cash in Hand"
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-6">
             <div className="space-y-2">
                <label className="text-sm font-medium">{t('accountForm.rootTypeLabel', 'Root Type')}</label>
                <select
                  name="rootType"
                  value={formData.rootType || 'Asset'}
                  onChange={handleChange}
                  disabled={isEditMode}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-950 disabled:opacity-50"
                >
                  <option value="Asset">{t('accountForm.rootAsset', 'Asset')}</option>
                  <option value="Liability">{t('accountForm.rootLiability', 'Liability')}</option>
                  <option value="Equity">{t('accountForm.rootEquity', 'Equity')}</option>
                  <option value="Income">{t('accountForm.rootIncome', 'Income')}</option>
                  <option value="Expense">{t('accountForm.rootExpense', 'Expense')}</option>
                </select>
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('accountForm.currencyLabel', 'Currency')}</label>
                <Input
                  name="currency"
                  value={formData.currency || 'USD'}
                  onChange={handleChange}
                  placeholder="e.g. USD"
                  disabled={isEditMode}
                  required
                />
              </div>
          </div>

          {!formData.isGroup && (
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('accountForm.typeLabel', 'Account Type')}</label>
              <select
                name="type"
                value={formData.type || 'Other'}
                onChange={handleChange}
                disabled={isEditMode}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-950 disabled:opacity-50"
              >
                <option value="Other">{t('accountForm.typeOther', 'Other (Default)')}</option>
                <option value="Bank">{t('accountForm.typeBank', 'Bank')}</option>
                <option value="Cash">{t('accountForm.typeCash', 'Cash')}</option>
                <option value="Receivable">{t('accountForm.typeReceivable', 'Receivable')}</option>
                <option value="Payable">{t('accountForm.typePayable', 'Payable')}</option>
                <option value="Stock">{t('accountForm.typeStock', 'Stock')}</option>
                <option value="COGS">{t('accountForm.typeCOGS', 'Cost of Goods Sold (COGS)')}</option>
                <option value="Tax">{t('accountForm.typeTax', 'Tax')}</option>
                <option value="Equity">{t('accountForm.typeEquity', 'Equity')}</option>
                <option value="Revenue">{t('accountForm.typeRevenue', 'Revenue')}</option>
                <option value="Expense">{t('accountForm.typeExpense', 'Expense')}</option>
                <option value="Depreciation">{t('accountForm.typeDepreciation', 'Depreciation')}</option>
                <option value="RoundOff">{t('accountForm.typeRoundOff', 'Round Off')}</option>
              </select>
            </div>
          )}

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('accountForm.parentLabel', 'Parent Group Account')}</label>
            <select
              name="parentAccountId"
              value={formData.parentAccountId || ''}
              onChange={handleChange}
              disabled={isEditMode || accountsQuery.status !== 'success'}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-950 disabled:opacity-50"
            >
              <option value="">{t('accountForm.accountNone', '-- No Parent --')}</option>
              {groupAccounts.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.code} — {account.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex flex-col gap-4">
             <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  name="isGroup"
                  checked={formData.isGroup || false}
                  onChange={handleChange}
                  id="isGroupAccount"
                  disabled={isEditMode}
                  className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950 disabled:opacity-50"
                />
                <label htmlFor="isGroupAccount" className="text-sm font-medium">
                  {t('accountForm.isGroup', 'Is Group Account?')} <span className="text-slate-500 font-normal">({t('accountForm.isGroupDesc', 'Group accounts cannot contain entries.')})</span>
                </label>
              </div>

              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  name="isActive"
                  checked={formData.isActive !== false}
                  onChange={handleChange}
                  id="isActiveAccount"
                  className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                />
                <label htmlFor="isActiveAccount" className="text-sm font-medium">
                  {t('accountForm.active', 'Active')}
                </label>
              </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {t('accountForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('accountForm.saving', 'Saving...') : t('accountForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
