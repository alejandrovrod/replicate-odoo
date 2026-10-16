import { useTranslation } from 'react-i18next'
import { useBatchAvailability } from '../api/useBatches'

interface BatchSelectorProps {
  itemId: string
  warehouseId: string
  /** Selected batch id ('' = none). */
  value: string
  onChange: (batchId: string) => void
  disabled?: boolean
}

/**
 * FEFO batch picker (batch-and-serial Fase 3, gap D5): dropdown of batches with on-hand
 * quantity in the warehouse, nearest expiry first. Expired or disabled rows render red
 * and are NOT selectable (the server rejects them with `batch_has_expired` regardless).
 */
export function BatchSelector({ itemId, warehouseId, value, onChange, disabled }: BatchSelectorProps) {
  const { t } = useTranslation('stock')
  const { batches, status } = useBatchAvailability(itemId, warehouseId)

  return (
    <select
      value={value}
      disabled={disabled || status === 'loading' || !itemId || !warehouseId}
      onChange={(e) => onChange(e.target.value)}
      aria-label={t('tracking.batchLabel', 'Batch')}
      className="w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900 disabled:opacity-50"
    >
      <option value="">
        {status === 'loading'
          ? t('tracking.batchLoading', 'Loading batches…')
          : status === 'error'
            ? t('tracking.batchUnavailable', 'Batches unavailable')
            : batches.length === 0
              ? t('tracking.batchEmpty', 'No batches with stock')
              : t('tracking.batchSelect', '-- Select batch (FEFO) --')}
      </option>
      {batches.map((batch) => {
        const blocked = batch.isExpired || batch.isDisabled
        const expiry = batch.expiryDate
          ? t('tracking.batchExpiry', 'exp {{date}}', { date: batch.expiryDate })
          : t('tracking.batchNoExpiry', 'no expiry')
        return (
          <option key={batch.batchId} value={batch.batchId} disabled={blocked}>
            {batch.batchNo} — {batch.qty.toLocaleString()}{' '}
            {t('tracking.batchUnits', 'u.')} ({expiry})
            {batch.isExpired ? ` · ${t('tracking.batchExpired', 'EXPIRED')}` : ''}
            {batch.isDisabled ? ` · ${t('tracking.batchDisabled', 'disabled')}` : ''}
          </option>
        )
      })}
    </select>
  )
}
