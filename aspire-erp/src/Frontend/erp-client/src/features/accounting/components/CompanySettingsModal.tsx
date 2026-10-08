import React, { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { useTenantStore } from '../../../store/useTenantStore'
import { apiClient } from '../../../api/client'
import { useAccountTree } from '../useAccountTree'

interface CompanySettings {
  id: string
  name: string
  frozenAccountsDate?: string | null
  defaultRetainedEarningsAccountId?: string | null
}

export function CompanySettingsModal({
  isOpen,
  onClose,
}: {
  isOpen: boolean
  onClose: () => void
}) {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore((s) => s.companyId)
  const { nodes, status: accountsStatus } = useAccountTree(companyId || '')

  const [settings, setSettings] = useState<CompanySettings | null>(null)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (isOpen && companyId) {
      loadSettings()
    }
  }, [isOpen, companyId])

  const loadSettings = async () => {
    try {
      setLoading(true)
      setError(null)
      const res = await apiClient.get<CompanySettings>(`/v1/companies/${companyId}`)
      setSettings(res.data)
    } catch (err: any) {
      setError(err.message || 'Error loading company settings')
    } finally {
      setLoading(false)
    }
  }

  const equityAccounts: { id: string; name: string; code: string }[] = []
  if (accountsStatus === 'success') {
    const walk = (treeNodes: any[]) => {
      for (const node of treeNodes) {
        if (!node.isGroup && node.isActive && node.rootType === 'Equity') {
          equityAccounts.push({ id: node.id, name: node.name, code: node.code })
        }
        if (node.children) walk(node.children)
      }
    }
    walk(nodes)
  }

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!settings || !companyId) return
    try {
      setSaving(true)
      setError(null)
      await apiClient.put(`/v1/companies/${companyId}`, {
        id: companyId,
        frozenAccountsDate: settings.frozenAccountsDate || null,
        defaultRetainedEarningsAccountId: settings.defaultRetainedEarningsAccountId || null
      })
      onClose()
    } catch (err: any) {
      setError(err.message || 'Error saving settings')
    } finally {
      setSaving(false)
    }
  }

  if (!isOpen) return null

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>
            {t('companySettings.title', 'Company Accounting Settings')}
          </DialogTitle>
        </DialogHeader>

        {loading || !settings ? (
          <div className="py-8 text-center text-slate-500">Loading...</div>
        ) : (
          <form onSubmit={handleSave} className="flex flex-col flex-1 overflow-hidden min-h-0">
            <div className="flex-1 overflow-y-auto pr-2 space-y-4">
              {error && <div className="text-sm text-red-600">{error}</div>}

              <div className="space-y-2">
                <label className="text-sm font-medium">
                  {t('companySettings.frozenDate', 'Frozen Accounts Date (Hard Period Lock)')}
                </label>
                <Input
                  type="date"
                  value={settings.frozenAccountsDate ? settings.frozenAccountsDate.split('T')[0] : ''}
                  onChange={(e) =>
                    setSettings({ ...settings, frozenAccountsDate: e.target.value })
                  }
                />
                <p className="text-xs text-slate-500">
                  {t('companySettings.frozenDateHelp', 'Vouchers dated on or before this day cannot be posted.')}
                </p>
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">
                  {t('companySettings.retainedEarnings', 'Default Retained Earnings Account')}
                </label>
                <select
                  value={settings.defaultRetainedEarningsAccountId || ''}
                  onChange={(e) =>
                    setSettings({
                      ...settings,
                      defaultRetainedEarningsAccountId: e.target.value,
                    })
                  }
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
                >
                  <option value="">-- {t('common.select', 'Select')} --</option>
                  {equityAccounts.map((acc) => (
                    <option key={acc.id} value={acc.id}>
                      {acc.code} - {acc.name}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-slate-500">
                  {t('companySettings.retainedEarningsHelp', 'Equity account used for period closing vouchers.')}
                </p>
              </div>
            </div>

            <DialogFooter className="pt-4 border-t border-slate-100">
              <Button type="button" variant="outline" onClick={onClose} disabled={saving}>
                {t('action.cancel', 'Cancel')}
              </Button>
              <Button type="submit" disabled={saving}>
                {saving ? t('state.saving', 'Saving...') : t('action.save', 'Save')}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
