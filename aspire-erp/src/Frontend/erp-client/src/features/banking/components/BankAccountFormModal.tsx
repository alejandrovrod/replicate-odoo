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
import type { BankAccount } from '../api/useBankAccounts'
import { useTranslation } from 'react-i18next'
import { useAccountTree } from '../../accounting/useAccountTree'
import { useCurrencies } from '../../accounting/api/useCurrencies'
import type { AccountTreeNode } from '../../accounting/types'

interface BankAccountFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<BankAccount>) => Promise<void>
  initialData?: BankAccount | null
  companyId: string
}

export function BankAccountFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
  companyId,
}: BankAccountFormModalProps) {
  const { t } = useTranslation('banking')
  const [formData, setFormData] = useState<Partial<BankAccount>>({
    companyId,
    isActive: true,
  })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const { nodes, status: accountsStatus } = useAccountTree(companyId || '')
  const currenciesQuery = useCurrencies()

  // Only active leaf accounts can back a bank profile (the III.3 postable notion).
  const postingAccounts: { id: string; name: string; code: string }[] = []
  if (accountsStatus === 'success') {
    const walk = (treeNodes: AccountTreeNode[]) => {
      for (const node of treeNodes) {
        if (!node.isGroup && node.isActive) {
          postingAccounts.push({ id: node.id, name: node.name, code: node.code })
        }
        walk(node.children)
      }
    }
    walk(nodes)
  }

  useEffect(() => {
    if (initialData) {
      setFormData(initialData)
    } else {
      setFormData({ companyId, isActive: true })
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

    if (!formData.accountName || !formData.bankName || !formData.accountNumber) {
      setError(t('bankAccountForm.errorRequired', 'Account name, bank and number are required.'))
      setIsSubmitting(false)
      return
    }
    if (!formData.glAccountId) {
      setError(t('bankAccountForm.errorGlRequired', 'A GL account is required.'))
      setIsSubmitting(false)
      return
    }

    try {
      // rowVersion/id/companyId travel inside formData on edits so the PUT carries the
      // original concurrency token; createBankAccount/updateBankAccount shape the wire body.
      await onSave({
        ...formData,
        glAccountId: formData.glAccountId,
        currencyId: formData.currencyId || undefined,
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('bankAccountForm.errorConflict', 'Concurrency conflict. Please refresh and try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('bankAccountForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('bankAccountForm.errorGeneric', 'An error occurred while saving.'))
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
            {initialData ? t('bankAccountForm.titleEdit', 'Edit Bank Account') : t('bankAccountForm.titleNew', 'New Bank Account')}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('bankAccountForm.nameLabel', 'Account Name *')}</label>
              <Input
                name="accountName"
                value={formData.accountName || ''}
                onChange={handleChange}
                placeholder="e.g. Main Operating Account"
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('bankAccountForm.bankLabel', 'Bank *')}</label>
              <Input
                name="bankName"
                value={formData.bankName || ''}
                onChange={handleChange}
                placeholder="e.g. JPMorgan Chase"
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('bankAccountForm.numberLabel', 'Account Number *')}</label>
              <Input
                name="accountNumber"
                value={formData.accountNumber || ''}
                onChange={handleChange}
                placeholder="e.g. 004920"
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('bankAccountForm.currencyLabel', 'Currency')}</label>
              <select
                name="currencyId"
                value={formData.currencyId || ''}
                onChange={handleChange}
                disabled={currenciesQuery.status !== 'success'}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
              >
                <option value="">{t('bankAccountForm.currencyNone', '-- Default (USD) --')}</option>
                {currenciesQuery.status === 'success' && currenciesQuery.items.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.code} — {c.symbol}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('bankAccountForm.glAccountLabel', 'GL Account *')}</label>
              <select
                name="glAccountId"
                value={formData.glAccountId || ''}
                onChange={handleChange}
                disabled={accountsStatus !== 'success'}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
              >
                <option value="">{t('bankAccountForm.accountNone', '-- Select Account --')}</option>
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
                id="isActiveBankAcct"
                className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
              />
              <label htmlFor="isActiveBankAcct" className="text-sm font-medium">
                {t('bankAccountForm.active', 'Active')}
              </label>
            </div>
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('bankAccountForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('bankAccountForm.saving', 'Saving...') : t('bankAccountForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
