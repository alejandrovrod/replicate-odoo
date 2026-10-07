import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { runDepreciation } from '../api/useAssets'
import { useTenantStore } from '../../../store/useTenantStore'

interface RunDepreciationModalProps {
  isOpen: boolean
  onClose: () => void
  onSuccess: () => void
}

export function RunDepreciationModal({ isOpen, onClose, onSuccess }: RunDepreciationModalProps) {
  const { t } = useTranslation('assets')
  const companyId = useTenantStore((state) => state.companyId)
  
  const [asOfDate, setAsOfDate] = useState(() => {
    const today = new Date()
    // Default to end of current month
    const endOfMonth = new Date(today.getFullYear(), today.getMonth() + 1, 0)
    return endOfMonth.toISOString().split('T')[0]
  })
  
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<{ bookedCount: number; totalBooked: number; voucherNo?: string } | null>(null)

  const handleRun = async () => {
    if (!companyId) return
    setLoading(true)
    setError(null)
    setResult(null)
    
    try {
      const res = await runDepreciation(companyId, asOfDate)
      setResult(res)
    } catch (err) {
      setError((err as Error).message || 'An error occurred during depreciation run.')
    } finally {
      setLoading(false)
    }
  }

  const handleClose = () => {
    setResult(null)
    setError(null)
    onClose()
  }

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('assets.depreciation.title', 'Run Depreciation')}</DialogTitle>
        </DialogHeader>
        
        <div className="space-y-4 py-4">
          {!result ? (
            <>
              <p className="text-sm text-slate-500">
                {t('assets.depreciation.description', 'Run depreciation for all active capitalized assets up to the selected date. This will generate a journal entry voucher.')}
              </p>
              
              <div className="space-y-2">
                <label className="text-sm font-medium text-slate-700">
                  {t('assets.depreciation.asOfDate', 'As of Date')}
                </label>
                <Input 
                  type="date" 
                  value={asOfDate} 
                  onChange={(e) => setAsOfDate(e.target.value)} 
                  disabled={loading}
                />
              </div>

              {error && (
                <div className="rounded-md bg-red-50 p-3 text-sm text-red-600">
                  {error}
                </div>
              )}
            </>
          ) : (
            <div className="space-y-3 rounded-md bg-emerald-50 p-4 text-emerald-800">
              <h3 className="font-semibold text-emerald-900">{t('assets.depreciation.success', 'Depreciation Run Successful')}</h3>
              <p className="text-sm">
                {t('assets.depreciation.bookedCount', 'Assets Booked:')} <strong>{result.bookedCount}</strong>
              </p>
              <p className="text-sm">
                {t('assets.depreciation.totalAmount', 'Total Amount Booked:')} <strong>{result.totalBooked.toLocaleString()}</strong>
              </p>
              {result.voucherNo && (
                <p className="text-sm">
                  {t('assets.depreciation.voucherNo', 'Voucher Generated:')} <strong>{result.voucherNo}</strong>
                </p>
              )}
            </div>
          )}
        </div>
        
        <DialogFooter>
          {!result ? (
            <>
              <Button variant="outline" onClick={handleClose} disabled={loading}>
                {t('action.cancel', { ns: 'common', defaultValue: 'Cancel' })}
              </Button>
              <Button onClick={handleRun} disabled={loading}>
                {loading ? t('state.saving', { ns: 'common', defaultValue: 'Saving...' }) : t('assets.depreciation.runBtn', 'Run')}
              </Button>
            </>
          ) : (
            <Button onClick={() => {
              handleClose()
              onSuccess()
            }}>
              {t('action.close', { ns: 'common', defaultValue: 'Close' })}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
