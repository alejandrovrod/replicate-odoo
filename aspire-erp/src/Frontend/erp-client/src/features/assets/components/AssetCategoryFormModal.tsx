import { useEffect, useState, useCallback } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError, apiClient } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { useTranslation } from 'react-i18next'
import type { AssetCategory } from '../api/useAssetCategories'


export interface AccountDto {
  id: string
  accountCode: string
  accountName: string
  isGroup: boolean
  isActive: boolean
}

interface AssetCategoryFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<AssetCategory>) => Promise<void>
  initialData?: AssetCategory | null
}

export function AssetCategoryFormModal({
  isOpen,
  onClose,
  onSave,
  initialData,
}: AssetCategoryFormModalProps) {
  const { t } = useTranslation('assets')
  const companyId = useTenantStore((state) => state.companyId)
  
  const [formData, setFormData] = useState<Partial<AssetCategory>>({
    companyId: companyId || '',
    isActive: true,
    isNonDepreciable: false,
  })
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [accounts, setAccounts] = useState<AccountDto[]>([])

  const fetchAccounts = useCallback(async () => {
    if (!companyId) return
    try {
      // The backend provides the COA as a tree; we need to flatten it
      const response = await apiClient.get<any[]>(`/v1/accounts/tree?companyId=${companyId}`)
      const flatList: AccountDto[] = []
      
      const flatten = (nodes: any[]) => {
        for (const node of nodes) {
          if (!node.isGroup && node.isActive) {
            flatList.push({
              id: node.id,
              accountCode: node.code,
              accountName: node.name,
              isGroup: node.isGroup,
              isActive: node.isActive,
            })
          }
          if (node.children?.length > 0) {
            flatten(node.children)
          }
        }
      }
      
      flatten(response.data)
      setAccounts(flatList)
    } catch (err) {
      console.error('Failed to fetch accounts', err)
    }
  }, [companyId])

  useEffect(() => {
    if (isOpen) {
      fetchAccounts()
    }
  }, [isOpen, fetchAccounts])

  useEffect(() => {
    if (initialData) {
      setFormData({
        ...initialData
      })
    } else {
      setFormData({
        companyId: companyId || '',
        isActive: true,
        isNonDepreciable: false,
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

    if (!formData.categoryName || !formData.fixedAssetAccountId || !formData.accumulatedDepreciationAccountId || !formData.depreciationExpenseAccountId) {
      setError(t('categoryForm.errorRequired', 'Category Name and core GL accounts are required.'))
      setIsSubmitting(false)
      return
    }

    try {
      await onSave({
        companyId: formData.companyId!,
        categoryName: formData.categoryName!,
        fixedAssetAccountId: formData.fixedAssetAccountId!,
        accumulatedDepreciationAccountId: formData.accumulatedDepreciationAccountId!,
        depreciationExpenseAccountId: formData.depreciationExpenseAccountId!,
        cwipAccountId: formData.cwipAccountId || undefined,
        gainOnDisposalAccountId: formData.gainOnDisposalAccountId || undefined,
        lossOnDisposalAccountId: formData.lossOnDisposalAccountId || undefined,
        isNonDepreciable: formData.isNonDepreciable || false,
        isActive: formData.isActive !== false,
        id: formData.id,
        rowVersion: formData.rowVersion,
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        setError(t('categoryForm.errorConflict', 'Concurrency conflict. Please try again.'))
      } else if (err instanceof Error) {
        setError(err.message || t('categoryForm.errorGeneric', 'An error occurred while saving.'))
      } else {
        setError(t('categoryForm.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  const isEditMode = !!formData.id

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-lg max-h-[90vh] flex flex-col">
        <DialogHeader>
          <DialogTitle className="text-xl">
            {isEditMode ? t('categoryForm.titleEdit', 'Edit Asset Category') : t('categoryForm.titleNew', 'New Asset Category')}
          </DialogTitle>
        </DialogHeader>
        
        <form onSubmit={handleSubmit} className="flex flex-col flex-1 min-h-0 overflow-hidden">
          <div className="flex-1 overflow-y-auto pr-4 space-y-6 pt-4 pb-4">
          {error && <div className="rounded-md bg-red-50 p-3 text-sm text-red-600">{error}</div>}

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('categoryForm.nameLabel', 'Category Name *')}</label>
            <Input
              name="categoryName"
              value={formData.categoryName || ''}
              onChange={handleChange}
              required
              placeholder={t('categoryForm.namePlaceholder', 'e.g. Computers')}
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('categoryForm.fixedAssetAccountLabel', 'Fixed Asset Account *')}</label>
            <select
              name="fixedAssetAccountId"
              value={formData.fixedAssetAccountId || ''}
              onChange={handleChange}
              required
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
            >
              <option value="">{t('categoryForm.selectAccount', '-- Select Account --')}</option>
              {accounts.map(a => (
                <option key={a.id} value={a.id}>{a.accountCode} — {a.accountName}</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('categoryForm.accumulatedDepreciationAccountLabel', 'Accumulated Depreciation Account *')}</label>
            <select
              name="accumulatedDepreciationAccountId"
              value={formData.accumulatedDepreciationAccountId || ''}
              onChange={handleChange}
              required
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
            >
              <option value="">{t('categoryForm.selectAccount', '-- Select Account --')}</option>
              {accounts.map(a => (
                <option key={a.id} value={a.id}>{a.accountCode} — {a.accountName}</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('categoryForm.depreciationExpenseAccountLabel', 'Depreciation Expense Account *')}</label>
            <select
              name="depreciationExpenseAccountId"
              value={formData.depreciationExpenseAccountId || ''}
              onChange={handleChange}
              required
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
            >
              <option value="">{t('categoryForm.selectAccount', '-- Select Account --')}</option>
              {accounts.map(a => (
                <option key={a.id} value={a.id}>{a.accountCode} — {a.accountName}</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('categoryForm.cwipAccountLabel', 'CWIP Account (Optional)')}</label>
            <select
              name="cwipAccountId"
              value={formData.cwipAccountId || ''}
              onChange={handleChange}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
            >
              <option value="">{t('categoryForm.selectAccount', '-- Select Account --')}</option>
              {accounts.map(a => (
                <option key={a.id} value={a.id}>{a.accountCode} — {a.accountName}</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('categoryForm.gainOnDisposalAccountLabel', 'Gain on Disposal Account (Optional)')}</label>
            <select
              name="gainOnDisposalAccountId"
              value={formData.gainOnDisposalAccountId || ''}
              onChange={handleChange}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
            >
              <option value="">{t('categoryForm.selectAccount', '-- Select Account --')}</option>
              {accounts.map(a => (
                <option key={a.id} value={a.id}>{a.accountCode} — {a.accountName}</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">{t('categoryForm.lossOnDisposalAccountLabel', 'Loss on Disposal Account (Optional)')}</label>
            <select
              name="lossOnDisposalAccountId"
              value={formData.lossOnDisposalAccountId || ''}
              onChange={handleChange}
              className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
            >
              <option value="">{t('categoryForm.selectAccount', '-- Select Account --')}</option>
              {accounts.map(a => (
                <option key={a.id} value={a.id}>{a.accountCode} — {a.accountName}</option>
              ))}
            </select>
          </div>

          <div className="flex flex-col gap-4">
             <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  name="isNonDepreciable"
                  checked={formData.isNonDepreciable || false}
                  onChange={handleChange}
                  id="isNonDepreciable"
                  className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950 disabled:opacity-50"
                />
                <label htmlFor="isNonDepreciable" className="text-sm font-medium">
                  {t('categoryForm.isNonDepreciable', 'Is Non-Depreciable?')} <span className="text-slate-500 font-normal">({t('categoryForm.isNonDepreciableDesc', 'e.g., Land')})</span>
                </label>
              </div>

              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  name="isActive"
                  checked={formData.isActive !== false}
                  onChange={handleChange}
                  id="isActiveCategory"
                  className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
                />
                <label htmlFor="isActiveCategory" className="text-sm font-medium">
                  {t('categoryForm.active', 'Active')}
                </label>
              </div>
          </div>

          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('categoryForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('categoryForm.saving', 'Saving...') : t('categoryForm.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
