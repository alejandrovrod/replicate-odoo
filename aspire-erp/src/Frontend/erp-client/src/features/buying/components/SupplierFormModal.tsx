import { useEffect, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../components/ui/Dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../components/ui/Tabs'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import type { Supplier, SupplierCompanyAccountInput, SupplierHoldType } from '../api/useSuppliers'
import { useTranslation } from 'react-i18next'
import { useCurrencies } from '../../accounting/api/useCurrencies'
import { useCompanies } from '../../accounting/api/useCompanies'
import type { Company } from '../../accounting/api/useCompanies'
import { useSupplierGroups } from '../api/useSupplierGroups'
import { useAccountTree } from '../../accounting/useAccountTree'
import { useTenantStore } from '../../../store/useTenantStore'
import type { AccountTreeNode } from '../../accounting/types'

/** Edit payload: the supplier fields plus the per-company account mappings (spec gap D2). */
export interface SupplierFormPayload extends Partial<Supplier> {
  companyAccounts?: SupplierCompanyAccountInput[]
}

interface SupplierFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: SupplierFormPayload) => Promise<void>
  initialData?: Supplier | null
}

interface AccountOption {
  id: string
  name: string
  code: string
}

const selectClass =
  'flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50 disabled:cursor-not-allowed'

/** Flattens the COA tree into leaf accounts, optionally narrowed by type (e.g. only Payable). */
function collectAccounts(nodes: AccountTreeNode[], onlyType?: AccountTreeNode['type']): AccountOption[] {
  const options: AccountOption[] = []
  const walk = (treeNodes: AccountTreeNode[]) => {
    for (const node of treeNodes) {
      if (!node.isGroup && (onlyType === undefined || node.type === onlyType)) {
        options.push({ id: node.id, name: node.name, code: node.code })
      }
      walk(node.children)
    }
  }
  walk(nodes)
  return options
}

interface CompanyAccountRowProps {
  company: Company
  value: SupplierCompanyAccountInput
  onChange: (next: SupplierCompanyAccountInput) => void
}

/**
 * One editable row of the multi-company accounts table. It owns its own COA request
 * (`useAccountTree`) so the parent can render a row per company without breaking the rules of
 * hooks (a hook cannot be called inside a loop).
 */
function CompanyAccountRow({ company, value, onChange }: CompanyAccountRowProps) {
  const { t } = useTranslation('buying')
  const { nodes, status } = useAccountTree(company.id)

  const payableAccounts = status === 'success' ? collectAccounts(nodes, 'Payable') : []
  const advanceAccounts = status === 'success' ? collectAccounts(nodes) : []

  return (
    <div className="rounded-md border border-slate-200 p-3 space-y-3">
      <div className="text-sm font-medium text-slate-900">{company.name}</div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <label className="text-xs font-medium text-slate-600">
            {t('supplierForm.companyAccountPayable', 'Payable Account')}
          </label>
          <select
            value={value.payableAccountId || ''}
            onChange={(e) => onChange({ ...value, payableAccountId: e.target.value })}
            disabled={status !== 'success'}
            className={selectClass}
          >
            <option value="">{t('supplierForm.accountNone', '-- Select Account --')}</option>
            {payableAccounts.map((account) => (
              <option key={account.id} value={account.id}>
                {account.code} — {account.name}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-1">
          <label className="text-xs font-medium text-slate-600">
            {t('supplierForm.companyAccountAdvance', 'Advance Account')}
          </label>
          <select
            value={value.advanceAccountId || ''}
            onChange={(e) => onChange({ ...value, advanceAccountId: e.target.value || null })}
            disabled={status !== 'success'}
            className={selectClass}
          >
            <option value="">{t('supplierForm.accountNone', '-- Select Account --')}</option>
            {advanceAccounts.map((account) => (
              <option key={account.id} value={account.id}>
                {account.code} — {account.name}
              </option>
            ))}
          </select>
        </div>
      </div>
    </div>
  )
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
  const [companyAccounts, setCompanyAccounts] = useState<SupplierCompanyAccountInput[]>([])
  const [activeTab, setActiveTab] = useState('general')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    setActiveTab('general')
    if (initialData) {
      setFormData(initialData)
      setCompanyAccounts(
        (initialData.accounts ?? []).map((account) => ({
          companyId: account.companyId,
          payableAccountId: account.payableAccountId,
          advanceAccountId: account.advanceAccountId ?? null,
        })),
      )
    } else {
      setFormData({ isActive: true, holdType: 'None' })
      setCompanyAccounts([])
    }
  }, [initialData, isOpen])

  const companyId = useTenantStore((state) => state.companyId)
  const { nodes, status: accountsStatus } = useAccountTree(companyId || '')
  const currenciesQuery = useCurrencies()
  const groupsQuery = useSupplierGroups()
  const companiesQuery = useCompanies()

  const payableAccounts = accountsStatus === 'success' ? collectAccounts(nodes, 'Payable') : []

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target
    const checked = (e.target as HTMLInputElement).checked
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }))
  }

  const handleHoldToggle = (checked: boolean) => {
    setFormData((prev) => ({
      ...prev,
      onHold: checked,
      // A hold always targets at least one action; releasing clears the type and date.
      holdType: checked ? (prev.holdType && prev.holdType !== 'None' ? prev.holdType : 'All') : 'None',
      releaseDate: checked ? prev.releaseDate ?? null : null,
    }))
  }

  const upsertCompanyAccount = (next: SupplierCompanyAccountInput) => {
    setCompanyAccounts((prev) => {
      const without = prev.filter((account) => account.companyId !== next.companyId)
      // A row with no payable account (and no advance) is treated as "not configured".
      if (!next.payableAccountId && !next.advanceAccountId) {
        return without
      }
      return [...without, next]
    })
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
        supplierGroupId: formData.supplierGroupId || null,
        releaseDate: formData.releaseDate || null,
        companyAccounts: companyAccounts.filter((account) => account.payableAccountId),
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

  const holdTypeOptions: { value: SupplierHoldType; label: string }[] = [
    { value: 'Invoices', label: t('supplierForm.holdTypeInvoices', 'Invoices') },
    { value: 'Payments', label: t('supplierForm.holdTypePayments', 'Payments') },
    { value: 'All', label: t('supplierForm.holdTypeAll', 'All documents') },
  ]

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {initialData ? t('supplierForm.titleEdit', 'Edit Supplier') : t('supplierForm.titleNew', 'New Supplier')}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <Tabs value={activeTab} onValueChange={setActiveTab} className="flex flex-col flex-1 overflow-hidden min-h-0">
            <TabsList>
              <TabsTrigger value="general">{t('supplierForm.tabGeneral', 'General')}</TabsTrigger>
              <TabsTrigger value="accounting">{t('supplierForm.tabAccounting', 'Accounting')}</TabsTrigger>
              <TabsTrigger value="governance">{t('supplierForm.tabGovernance', 'Governance & Hold')}</TabsTrigger>
            </TabsList>

            <div className="flex-1 overflow-y-auto pr-2 mt-3">
              {error && <div className="text-sm text-red-600 mb-3">{error}</div>}

              <TabsContent value="general" className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('supplierForm.codeLabel', 'Supplier Code *')}</label>
                  <Input
                    name="code"
                    value={formData.code || ''}
                    onChange={handleChange}
                    placeholder={t('supplierForm.codePlaceholder', 'e.g. SUP-001')}
                    required
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('supplierForm.nameLabel', 'Supplier Name *')}</label>
                  <Input
                    name="name"
                    value={formData.name || ''}
                    onChange={handleChange}
                    placeholder={t('supplierForm.namePlaceholder', 'e.g. Acme Corp')}
                    required
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('supplierForm.taxIdLabel', 'Tax ID')}</label>
                  <Input
                    name="taxId"
                    value={formData.taxId || ''}
                    onChange={handleChange}
                    placeholder={t('supplierForm.taxIdPlaceholder', 'Optional Tax ID')}
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('supplierForm.groupLabel', 'Supplier Group')}</label>
                  <select
                    name="supplierGroupId"
                    value={formData.supplierGroupId || ''}
                    onChange={handleChange}
                    disabled={groupsQuery.status !== 'success'}
                    className={selectClass}
                  >
                    <option value="">{t('supplierForm.groupNone', '-- No group --')}</option>
                    {groupsQuery.status === 'success' && groupsQuery.items.map((group) => (
                      <option key={group.id} value={group.id}>
                        {group.code} — {group.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('supplierForm.currencyLabel', 'Currency')}</label>
                  <select
                    name="currencyId"
                    value={formData.currencyId || ''}
                    onChange={handleChange}
                    disabled={currenciesQuery.status !== 'success'}
                    className={selectClass}
                  >
                    <option value="">{t('supplierForm.currencyNone', '-- Default (USD) --')}</option>
                    {currenciesQuery.status === 'success' && currenciesQuery.items.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.code} — {c.symbol}
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
                    id="isActiveSupp"
                    className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                  />
                  <label htmlFor="isActiveSupp" className="text-sm font-medium">
                    {t('supplierForm.active', 'Active')}
                  </label>
                </div>
              </TabsContent>

              <TabsContent value="accounting" className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">
                    {t('supplierForm.payableAccountLabel', 'Default Payable Account')}
                  </label>
                  <select
                    name="defaultPayableAccountId"
                    value={formData.defaultPayableAccountId || ''}
                    onChange={handleChange}
                    disabled={accountsStatus !== 'success'}
                    className={selectClass}
                  >
                    <option value="">{t('supplierForm.accountNone', '-- Select Account --')}</option>
                    {payableAccounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.code} — {account.name}
                      </option>
                    ))}
                  </select>
                  <p className="text-xs text-slate-500">
                    {t('supplierForm.payableAccountHint', 'Used for single-company tenants; per-company mappings below take precedence.')}
                  </p>
                </div>

                <div className="space-y-2">
                  <div className="text-sm font-medium">{t('supplierForm.companyAccountsTitle', 'Accounts by Company')}</div>
                  {companiesQuery.status === 'error' && (
                    <div className="text-sm text-red-600">
                      {t('supplierForm.companyAccountsLoadError', 'Could not load the company list.')}
                    </div>
                  )}
                  {companiesQuery.status !== 'error' && companiesQuery.items.length === 0 && (
                    <div className="text-sm text-slate-500">
                      {t('supplierForm.companyAccountsEmpty', 'No companies available.')}
                    </div>
                  )}
                  <div className="space-y-2">
                    {companiesQuery.items.map((company) => (
                      <CompanyAccountRow
                        key={company.id}
                        company={company}
                        value={
                          companyAccounts.find((account) => account.companyId === company.id) ?? {
                            companyId: company.id,
                            payableAccountId: '',
                            advanceAccountId: null,
                          }
                        }
                        onChange={upsertCompanyAccount}
                      />
                    ))}
                  </div>
                </div>
              </TabsContent>

              <TabsContent value="governance" className="space-y-4">
                <div className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    name="onHold"
                    checked={formData.onHold === true}
                    onChange={(e) => handleHoldToggle(e.target.checked)}
                    id="onHoldSupp"
                    className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                  />
                  <label htmlFor="onHoldSupp" className="text-sm font-medium">
                    {t('supplierForm.onHoldLabel', 'On Hold')}
                  </label>
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('supplierForm.holdTypeLabel', 'Hold blocks')}</label>
                  <select
                    name="holdType"
                    value={formData.onHold ? formData.holdType ?? 'All' : 'None'}
                    onChange={handleChange}
                    disabled={!formData.onHold}
                    className={selectClass}
                  >
                    {formData.onHold ? (
                      holdTypeOptions.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))
                    ) : (
                      <option value="None">{t('supplierForm.holdTypeNone', 'No hold')}</option>
                    )}
                  </select>
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('supplierForm.releaseDateLabel', 'Release Date')}</label>
                  <Input
                    type="date"
                    name="releaseDate"
                    value={formData.releaseDate || ''}
                    onChange={handleChange}
                    disabled={!formData.onHold}
                  />
                  <p className="text-xs text-slate-500">
                    {t('supplierForm.releaseDateHint', 'The hold is lifted automatically after this date; leave empty to keep it indefinitely.')}
                  </p>
                </div>

                {initialData && (
                  <div className="flex items-center space-x-2">
                    <input
                      type="checkbox"
                      name="isFrozen"
                      checked={formData.isFrozen === true}
                      onChange={handleChange}
                      id="isFrozenSupp"
                      className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                    />
                    <label htmlFor="isFrozenSupp" className="text-sm font-medium">
                      {t('supplierForm.frozenLabel', 'Frozen (block record edits)')}
                    </label>
                  </div>
                )}
              </TabsContent>
            </div>
          </Tabs>

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
