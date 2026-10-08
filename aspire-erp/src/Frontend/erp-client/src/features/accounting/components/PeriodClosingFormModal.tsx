import React, { useState } from 'react'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { useTranslation } from 'react-i18next'
import { useTenantStore } from '../../../store/useTenantStore'
import { useAccountTree } from '../useAccountTree'
import { apiClient } from '../../../api/client'

export function PeriodClosingFormModal({ isOpen, onClose, onSaved }: { isOpen: boolean, onClose: () => void, onSaved: () => void }) {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore(s => s.companyId)
  const { nodes, status: accountsStatus } = useAccountTree(companyId || '')
  
  const [formData, setFormData] = useState({ postingDate: '', retainedEarningsAccountId: '', remarks: '' })
  const [submitting, setSubmitting] = useState(false)

  const equityAccounts: any[] = []
  if (accountsStatus === 'success') {
    const walk = (treeNodes: any[]) => {
      for (const node of treeNodes) {
        if (!node.isGroup && node.isActive && node.rootType === 'Equity') equityAccounts.push(node)
        if (node.children) walk(node.children)
      }
    }
    walk(nodes)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    try {
      await apiClient.post('/v1/periodclosing', { ...formData, companyId })
      onSaved()
      onClose()
    } catch {
      // error
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('periodClosing.newTitle', 'New Period Closing')}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4 pt-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">Posting Date</label>
              <Input type="date" required value={formData.postingDate} onChange={e => setFormData({...formData, postingDate: e.target.value})} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Retained Earnings Account</label>
              <select required className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3" value={formData.retainedEarningsAccountId} onChange={e => setFormData({...formData, retainedEarningsAccountId: e.target.value})}>
                <option value="">-- Select --</option>
                {equityAccounts.map(a => <option key={a.id} value={a.id}>{a.code} - {a.name}</option>)}
              </select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Remarks</label>
              <Input value={formData.remarks} onChange={e => setFormData({...formData, remarks: e.target.value})} />
            </div>
          </div>
          <DialogFooter className="pt-4">
            <Button variant="outline" onClick={onClose} type="button">Cancel</Button>
            <Button type="submit" disabled={submitting}>Save</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
