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
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useTranslation } from 'react-i18next'
import { useCurrencies } from '../api/useCurrencies'
import type { AccountTreeNode, AccountType } from '../types'
import { collectGroupAccounts } from '../types'

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
  /**
   * Tri-state reparent mirror of `UpdateAccountCommand` (COA-02/COA-03).
   * Only meaningful on edits; create infers it from `parentAccountId`.
   */
  changeParent?: boolean
  /** RM-09 FK to the currency catalog (null = "USD" fallback server-side). */
  currencyId?: string | null
  /**
   * Optimistic-concurrency token (COA-G6): arrives from the tree node via
   * `initialData`, echoes back on PUT, and the container's `reload()` fetches
   * the fresh token afterwards. Never edited by the user.
   */
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

type TouchedFields = Partial<Record<'accountCode' | 'accountName' | 'rootType' | 'parentAccountId', boolean>>

const EMPTY_FORM = (companyId: string): Partial<AccountFormData> => ({
  companyId,
  isActive: true,
  isGroup: false,
  currencyId: undefined,
  rootType: 'Asset',
  type: 'Other',
})

/**
 * Reactive COA form (Container-Presentational: pure presentational half; the
 * container owns persistence + reload). Required fields validate instantly on
 * every keystroke once touched, and the submit path marks everything touched
 * so untouched invalid fields still surface. Domain (RFC 7807) feedback arrives
 * via `translateErrorCode(code, detail)` and stays visible in the modal while
 * the container independently raises its 409 reload banner — neither surface
 * swallows the other.
 */
export function AccountFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
  companyId,
  accountsQuery,
}: AccountFormModalProps) {
  const { t, i18n } = useTranslation('accounting')

  const [formData, setFormData] = useState<Partial<AccountFormData>>(() => EMPTY_FORM(companyId))
  const [touched, setTouched] = useState<TouchedFields>({})
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Global currency catalog for the functional-currency selector (RM-09).
  const currenciesQuery = useCurrencies()

  // Parent selector is pre-filtered to group accounts only (COA-01 defense in
  // depth; the domain's `parent_is_not_group` stays authoritative).
  const groupAccounts = useMemo(
    () =>
      accountsQuery.status === 'success' ? collectGroupAccounts(accountsQuery.nodes) : [],
    [accountsQuery.nodes, accountsQuery.status],
  )
  const groupAccountIds = useMemo(
    () => new Set(groupAccounts.map((account) => account.id)),
    [groupAccounts],
  )

  useEffect(() => {
    if (isOpen) {
      setFormData(initialData ? { ...initialData } : EMPTY_FORM(companyId))
      setTouched({})
      setError(null)
    }
  }, [initialData, companyId, isOpen])

  const isEditMode = !!formData.id

  // Instant required-field validation (accountCode, accountName, rootType).
  // isGroup is a boolean switch — always "valid", but included in the reactive
  // model so the submit payload can never drop it.
  const fieldErrors = useMemo(() => {
    const errors: Partial<Record<'accountCode' | 'accountName' | 'rootType' | 'parentAccountId', string>> = {}
    if (!formData.accountCode?.trim()) {
      errors.accountCode = t('accountForm.errorCodeRequired', 'Account code is required.')
    }
    if (!formData.accountName?.trim()) {
      errors.accountName = t('accountForm.errorNameRequired', 'Account name is required.')
    }
    if (!formData.rootType) {
      errors.rootType = t('accountForm.errorRootTypeRequired', 'Root type is required.')
    }
    if (
      formData.parentAccountId &&
      accountsQuery.status === 'success' &&
      !groupAccountIds.has(formData.parentAccountId)
    ) {
      errors.parentAccountId = t('accountForm.errorParentNotGroup', 'Parent must be a group account.')
    }
    return errors
  }, [formData, groupAccountIds, accountsQuery.status, t])

  const visibleErrors = useMemo(() => {
    const visible: typeof fieldErrors = {}
    for (const [key, message] of Object.entries(fieldErrors)) {
      const field = key as keyof TouchedFields
      if (touched[field]) visible[field] = message
    }
    return visible
  }, [fieldErrors, touched])

  const isValid = Object.keys(fieldErrors).length === 0

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target
    const checked = (e.target as HTMLInputElement).checked
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }))
    if (name === 'accountCode' || name === 'accountName' || name === 'rootType' || name === 'parentAccountId') {
      setTouched((prev) => ({ ...prev, [name]: true }))
    }
  }

  const handleBlur = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name } = e.target
    if (name === 'accountCode' || name === 'accountName' || name === 'rootType' || name === 'parentAccountId') {
      setTouched((prev) => ({ ...prev, [name]: true }))
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    // Reactive gate: reveal every field error at once, then stop before the wire.
    setTouched({ accountCode: true, accountName: true, rootType: true, parentAccountId: true })
    if (!isValid) {
      setError(t('accountForm.errorRequired', 'Code and Name are required.'))
      return
    }
    setIsSubmitting(true)

    try {
      await onSave({
        companyId: formData.companyId!,
        accountCode: formData.accountCode!.trim(),
        accountName: formData.accountName!.trim(),
        rootType: formData.rootType as AccountFormData['rootType'],
        type: formData.isGroup ? 'Other' : (formData.type || 'Other'),
        isGroup: formData.isGroup || false,
        isActive: formData.isActive !== false,
        parentAccountId: formData.parentAccountId || undefined,
        changeParent: isEditMode ? formData.changeParent : undefined,
        currencyId: formData.currencyId ?? null,
        id: formData.id,
        // Tree → modal → PUT echo of the concurrency token (COA-G6).
        rowVersion: formData.rowVersion,
      })
      onClose()
    } catch (err: unknown) {
      // Domain feedback: map invariant RFC7807 code first, fall back to detail.
      if (err instanceof ApiError) {
        const mapped = translateErrorCode(i18n, err.code, err.message)
        setError(
          mapped ||
            (err.status === 409
              ? t('accountForm.errorConflict', 'Concurrency conflict or duplicate code. Please try again.')
              : t('accountForm.errorGeneric', 'An error occurred while saving.')),
        )
      } else if (err instanceof Error) {
        setError(err.message || t('accountForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('accountForm.errorGeneric', 'An error occurred while saving.'))
      }
      // NOTE: intentionally not rethrown — the container's own catch already
      // captured 409 into its reload banner before this catch ran, so both
      // surfaces stay visible and no unhandled rejection escapes onSubmit.
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="text-xl">
            {isEditMode ? t('accountForm.titleEdit', 'Edit Account') : t('accountForm.titleNew', 'New Account')}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} noValidate className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-6 pt-4">
            {error && <div role="alert" data-testid="account-form-error" className="rounded-md bg-red-50 p-3 text-sm text-red-600">{error}</div>}

            <div className="space-y-6">
              <div className="space-y-2">
                <label htmlFor="account-code-input" className="text-sm font-medium">{t('accountForm.codeLabel', 'Account Code *')}</label>
                <Input
                  id="account-code-input"
                  name="accountCode"
                  data-testid="account-code-input"
                  value={formData.accountCode || ''}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  placeholder="e.g. 1100"
                  required
                  disabled={isEditMode} // Cannot change code after creation in this implementation
                  aria-invalid={visibleErrors.accountCode ? true : undefined}
                  aria-describedby={visibleErrors.accountCode ? 'account-code-error' : undefined}
                />
                {visibleErrors.accountCode && (
                  <p id="account-code-error" role="alert" className="text-xs text-red-600">{visibleErrors.accountCode}</p>
                )}
              </div>

              <div className="space-y-2">
                <label htmlFor="account-name-input" className="text-sm font-medium">{t('accountForm.nameLabel', 'Account Name *')}</label>
                <Input
                  id="account-name-input"
                  name="accountName"
                  data-testid="account-name-input"
                  value={formData.accountName || ''}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  placeholder="e.g. Cash in Hand"
                  required
                  aria-invalid={visibleErrors.accountName ? true : undefined}
                  aria-describedby={visibleErrors.accountName ? 'account-name-error' : undefined}
                />
                {visibleErrors.accountName && (
                  <p id="account-name-error" role="alert" className="text-xs text-red-600">{visibleErrors.accountName}</p>
                )}
              </div>
            </div>

            <div className="space-y-6">
               <div className="space-y-2">
                  <label htmlFor="account-roottype-select" className="text-sm font-medium">{t('accountForm.rootTypeLabel', 'Root Type')}</label>
                  <select
                    id="account-roottype-select"
                    name="rootType"
                    data-testid="account-roottype-select"
                    value={formData.rootType || 'Asset'}
                    onChange={handleChange}
                    onBlur={handleBlur}
                    disabled={isEditMode}
                    required
                    aria-invalid={visibleErrors.rootType ? true : undefined}
                    aria-describedby={visibleErrors.rootType ? 'account-roottype-error' : undefined}
                    className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
                  >
                    <option value="Asset">{t('accountForm.rootAsset', 'Asset')}</option>
                    <option value="Liability">{t('accountForm.rootLiability', 'Liability')}</option>
                    <option value="Equity">{t('accountForm.rootEquity', 'Equity')}</option>
                    <option value="Income">{t('accountForm.rootIncome', 'Income')}</option>
                    <option value="Expense">{t('accountForm.rootExpense', 'Expense')}</option>
                  </select>
                  {visibleErrors.rootType && (
                    <p id="account-roottype-error" role="alert" className="text-xs text-red-600">{visibleErrors.rootType}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <label htmlFor="account-currency-select" className="text-sm font-medium">{t('accountForm.currencyLabel', 'Currency')}</label>
                  <select
                    id="account-currency-select"
                    name="currencyId"
                    data-testid="account-currency-select"
                    value={formData.currencyId || ''}
                    onChange={handleChange}
                    disabled={isEditMode || currenciesQuery.status !== 'success'}
                    className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
                  >
                    <option value="">{t('accountForm.currencyNone', '-- Default (USD) --')}</option>
                    {currenciesQuery.status === 'success' && currenciesQuery.items.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.code} — {c.symbol} {c.fractionName}
                      </option>
                    ))}
                  </select>
                </div>
            </div>

            {!formData.isGroup && (
              <div className="space-y-2">
                <label htmlFor="account-type-select" className="text-sm font-medium">{t('accountForm.typeLabel', 'Account Type')}</label>
                <select
                  id="account-type-select"
                  name="type"
                  data-testid="account-type-select"
                  value={formData.type || 'Other'}
                  onChange={handleChange}
                  disabled={isEditMode}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
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
              <label htmlFor="account-parent-select" className="text-sm font-medium">{t('accountForm.parentLabel', 'Parent Group Account')}</label>
              <select
                id="account-parent-select"
                name="parentAccountId"
                data-testid="account-parent-select"
                value={formData.parentAccountId || ''}
                onChange={handleChange}
                onBlur={handleBlur}
                disabled={isEditMode || accountsQuery.status !== 'success'}
                aria-invalid={visibleErrors.parentAccountId ? true : undefined}
                aria-describedby={visibleErrors.parentAccountId ? 'account-parent-error' : undefined}
                className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
              >
                <option value="">{t('accountForm.accountNone', '-- No Parent --')}</option>
                {groupAccounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
              </select>
              {visibleErrors.parentAccountId && (
                <p id="account-parent-error" role="alert" className="text-xs text-red-600">{visibleErrors.parentAccountId}</p>
              )}
            </div>

            <div className="flex flex-col gap-4 pb-4">
               <div className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    name="isGroup"
                    data-testid="account-isgroup-switch"
                    checked={formData.isGroup || false}
                    onChange={handleChange}
                    id="isGroupAccount"
                    disabled={isEditMode}
                    aria-describedby="isGroupAccountHelp"
                    className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950 disabled:opacity-50"
                  />
                  <label htmlFor="isGroupAccount" className="text-sm font-medium">
                    {t('accountForm.isGroup', 'Is Group Account?')} <span className="text-slate-500 font-normal">({t('accountForm.isGroupDesc', 'Group accounts cannot contain entries.')})</span>
                  </label>
                </div>
                <p id="isGroupAccountHelp" className="text-xs text-slate-500">
                  {t('accountForm.isGroupHelp', 'Group accounts organize children and cannot hold postings. Leaf accounts hold balances.')}
                </p>

                <div className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    name="isActive"
                    data-testid="account-isactive-switch"
                    checked={formData.isActive !== false}
                    onChange={handleChange}
                    id="isActiveAccount"
                    aria-describedby="isActiveAccountHelp"
                    className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                  />
                  <label htmlFor="isActiveAccount" className="text-sm font-medium">
                    {t('accountForm.active', 'Active')}
                  </label>
                </div>
                <p id="isActiveAccountHelp" className="text-xs text-slate-500">
                  {t('accountForm.activeDesc', 'Account is available for selection.')}
                </p>
            </div>
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" data-testid="account-cancel-btn" onClick={onClose}>
              {t('accountForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" data-testid="account-save-btn" disabled={isSubmitting || !isValid}>
              {isSubmitting ? t('accountForm.saving', 'Saving...') : t('accountForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
