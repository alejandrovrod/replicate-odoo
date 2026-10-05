import { useState } from 'react'
import { CheckCircle2, PackagePlus } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { apiClient } from '../../api/client'
import { useErpAction } from '../../lib/useErpAction'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { useTenantStore } from '../../store/useTenantStore'

interface PurchaseOrder {
  id: string
  supplierId: string
  supplierName: string
  items: {
    itemId: string
    itemName: string
    quantity: number
    receivedQuantity: number
    rate: number
  }[]
}

interface PurchaseReceiptModalProps {
  order: PurchaseOrder | null
  onClose: () => void
  onSuccess: () => void
}

export function PurchaseReceiptModal({ order, onClose, onSuccess }: PurchaseReceiptModalProps) {
  const { t, i18n } = useTranslation('buying')
  const companyId = useTenantStore((state) => state.companyId)
  // Hardcoded default warehouse for demo
  const [warehouseId] = useState('11111111-1111-1111-1111-111111111111')

  const { state: submitState, dispatch: submitReceipt, isPending } = useErpAction(
    async () => {
      if (!order) return null

      // Build payload for PostPurchaseReceiptCommand
      const payload = {
        companyId,
        warehouseId,
        supplierId: order.supplierId,
        purchaseOrderId: order.id,
        postingDate: new Date().toISOString().split('T')[0],
        lines: order.items
          .filter(i => i.quantity - i.receivedQuantity > 0)
          .map(i => ({
            itemId: i.itemId,
            qty: i.quantity - i.receivedQuantity,
            rate: i.rate
          }))
      }

      const response = await apiClient.post('/v1/purchasereceipts', payload)
      return response.data
    }
  )

  if (!order) return null

  if (submitState.isSuccess) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm">
        <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl">
          <div className="text-center">
            <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-indigo-100">
              <CheckCircle2 className="h-8 w-8 text-indigo-600" />
            </div>
            <h2 className="mt-4 text-xl font-bold text-slate-900">{t('receipt.posted')}</h2>
            <p className="mt-2 text-sm text-slate-600">
              {t('receipt.created', { number: submitState.data?.receiptNumber })}
            </p>
            <button
              onClick={() => {
                onSuccess()
                onClose()
              }}
              className="mt-6 w-full rounded-lg bg-slate-900 px-4 py-2.5 font-semibold text-white hover:bg-slate-800"
            >
              {t('receipt.done')}
            </button>
          </div>
        </div>
      </div>
    )
  }

  const pendingItems = order.items.filter(i => i.quantity - i.receivedQuantity > 0)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl">
        <div className="flex items-center justify-between border-b border-slate-100 pb-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-indigo-50">
              <PackagePlus className="h-5 w-5 text-indigo-600" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900">{t('receipt.title')}</h2>
              <p className="text-xs text-slate-500">{t('receipt.forOrder', { id: order.id })}</p>
            </div>
          </div>
          <button onClick={onClose} aria-label={t('receipt.close')} className="rounded-full p-2 text-slate-400 hover:bg-slate-50">
            x
          </button>
        </div>

        <div className="py-4">
          <h3 className="mb-2 text-sm font-semibold text-slate-700">{t('receipt.pendingItems')}</h3>
          <ul className="divide-y divide-slate-100 rounded-lg border border-slate-100">
            {pendingItems.map(item => (
              <li key={item.itemId} className="flex justify-between px-4 py-3 text-sm">
                <span className="font-medium text-slate-900">{item.itemName}</span>
                <span className="text-slate-600">{t('receipt.qty', { qty: item.quantity - item.receivedQuantity })}</span>
              </li>
            ))}
            {pendingItems.length === 0 && (
              <li className="px-4 py-3 text-sm text-slate-500">{t('receipt.noPending')}</li>
            )}
          </ul>

          {submitState.error && (
            <div className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-600">
              {translateErrorCode(i18n, submitState.errorCode, submitState.error)}
            </div>
          )}
        </div>

        <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4">
          <button
            onClick={onClose}
            className="rounded-lg px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50"
          >
            {t('receipt.cancel')}
          </button>
          <button
            onClick={() => submitReceipt(undefined)}
            disabled={isPending || pendingItems.length === 0}
            className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-xs hover:bg-indigo-700 disabled:opacity-50"
          >
            {isPending ? t('receipt.posting') : t('receipt.post')}
          </button>
        </div>
      </div>
    </div>
  )
}
