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
import { useCurrencies } from '../../accounting/api/useCurrencies'
import { useAccountTree } from '../../accounting/useAccountTree'
import type { AccountTreeNode } from '../../accounting/types'

interface CustomerFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<Customer>) => Promise<void>
  initialData?: Customer | null
  companyId: string
}

function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-500 pt-2">
      {children}
    </h4>
  )
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

  const { nodes, status: accountsStatus } = useAccountTree(companyId || '')
  const currenciesQuery = useCurrencies()

  const receivableAccounts: { id: string; name: string; code: string }[] = []
  if (accountsStatus === 'success') {
    const walk = (treeNodes: AccountTreeNode[]) => {
      for (const node of treeNodes) {
        if (!node.isGroup && node.isActive && node.type === 'Receivable') {
          receivableAccounts.push({ id: node.id, name: node.name, code: node.code })
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
      [name]: type === 'checkbox' ? checked : type === 'number' ? parseFloat(value) : value,
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)

    if (!formData.name || !formData.code) {
      setError(t('customerForm.errorRequired', 'Name and Code are required.'))
      setIsSubmitting(false)
      return
    }

    try {
      // rowVersion/id/companyId travel inside formData on edits so the PUT carries the
      // original concurrency token; createCustomer/updateCustomer shape the wire body.
      await onSave({
        ...formData,
        defaultReceivableAccountId: formData.defaultReceivableAccountId || undefined,
        currencyId: formData.currencyId || undefined,
      })
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

  const selectClassName =
    'flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50'

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>
            {initialData ? t('customerForm.titleEdit', 'Edit Customer') : t('customerForm.titleNew', 'New Customer')}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <SectionTitle>{t('customerForm.sectionGeneral', 'General')}</SectionTitle>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.codeLabel', 'Customer Code *')}</label>
                <Input
                  name="code"
                  value={formData.code || ''}
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

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.typeLabel', 'Customer Type')}</label>
                <select name="customerType" value={formData.customerType || 'Company'} onChange={handleChange} className={selectClassName}>
                  <option value="Company">{t('customerForm.type_Company', 'Company')}</option>
                  <option value="Individual">{t('customerForm.type_Individual', 'Individual')}</option>
                  <option value="Partnership">{t('customerForm.type_Partnership', 'Partnership')}</option>
                </select>
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
                <label className="text-sm font-medium">{t('customerForm.groupLabel', 'Customer Group')}</label>
                <Input
                  name="customerGroup"
                  value={formData.customerGroup || ''}
                  onChange={handleChange}
                  placeholder="e.g. Retail"
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.territoryLabel', 'Territory')}</label>
                <Input
                  name="territory"
                  value={formData.territory || ''}
                  onChange={handleChange}
                  placeholder="e.g. North"
                />
              </div>
            </div>

            <SectionTitle>{t('customerForm.sectionContact', 'Contact')}</SectionTitle>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.contactPersonLabel', 'Contact Person')}</label>
                <Input
                  name="contactPerson"
                  value={formData.contactPerson || ''}
                  onChange={handleChange}
                  placeholder="e.g. Jane Doe"
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.phoneLabel', 'Phone')}</label>
                <Input
                  name="phone"
                  value={formData.phone || ''}
                  onChange={handleChange}
                  placeholder="e.g. +54 11 5555-5555"
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.emailLabel', 'Email')}</label>
                <Input
                  name="email"
                  type="email"
                  value={formData.email || ''}
                  onChange={handleChange}
                  placeholder="e.g. billing@acme.com"
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.websiteLabel', 'Website')}</label>
                <Input
                  name="website"
                  value={formData.website || ''}
                  onChange={handleChange}
                  placeholder="e.g. https://acme.com"
                />
              </div>

              <div className="space-y-2 sm:col-span-2">
                <label className="text-sm font-medium">{t('customerForm.billingAddressLabel', 'Billing Address')}</label>
                <Input
                  name="billingAddress"
                  value={formData.billingAddress || ''}
                  onChange={handleChange}
                  placeholder="e.g. Av. Siempre Viva 123, CABA"
                />
              </div>
            </div>

            <SectionTitle>{t('customerForm.sectionBilling', 'Billing & Credit')}</SectionTitle>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.paymentTermsLabel', 'Payment Terms')}</label>
                <Input
                  name="paymentTerms"
                  value={formData.paymentTerms || ''}
                  onChange={handleChange}
                  placeholder="e.g. Net 30"
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.currencyLabel', 'Currency')}</label>
                <select
                  name="currencyId"
                  value={formData.currencyId || ''}
                  onChange={handleChange}
                  disabled={currenciesQuery.status !== 'success'}
                  className={selectClassName}
                >
                  <option value="">{t('customerForm.currencyNone', '-- Default (USD) --')}</option>
                  {currenciesQuery.status === 'success' && currenciesQuery.items.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.symbol}
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">{t('customerForm.receivableAccountLabel', 'Default Receivable Account')}</label>
                <select
                  name="defaultReceivableAccountId"
                  value={formData.defaultReceivableAccountId || ''}
                  onChange={handleChange}
                  disabled={accountsStatus !== 'success'}
                  className={selectClassName}
                >
                  <option value="">{t('customerForm.accountNone', '-- Select Account --')}</option>
                  {receivableAccounts.map((account) => (
                    <option key={account.id} value={account.id}>
                      {account.code} — {account.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-2 sm:col-span-2">
                <label className="text-sm font-medium">{t('customerForm.detailsLabel', 'Internal Notes')}</label>
                <Input
                  name="customerDetails"
                  value={formData.customerDetails || ''}
                  onChange={handleChange}
                  placeholder={t('customerForm.detailsPlaceholder', 'Internal notes about this customer')}
                />
              </div>
            </div>

            <div className="flex items-center space-x-2 pb-4">
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
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
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
