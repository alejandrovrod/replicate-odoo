import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useSalesOrders } from '../api/useSalesOrders'
import { useFlatWarehouses, useItems } from '../../stock/useStockData'
import { MAX_PAGE_SIZE } from '../../../lib/pagination'
import { BatchSelector } from '../../stock/components/BatchSelector'
import { SerialNoPickerModal } from '../../stock/components/SerialNoPickerModal'

interface TrackingDraft {
  batchId: string
  serials: string[]
}

interface DeliveryNoteFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: {
    salesOrderId: string
    warehouseId: string
    postingDate: string
    lines: { salesOrderItemId: string; itemId: string; qty: number; batchId?: string | null; serialNumbers?: string[] }[]
  }) => Promise<void>
  companyId: string
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export function DeliveryNoteFormModal({ isOpen, onClose, onSave, companyId }: DeliveryNoteFormModalProps) {
  const { t, i18n } = useTranslation('selling')
  const [salesOrderId, setSalesOrderId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [postingDate, setPostingDate] = useState(todayIso())
  const [quantities, setQuantities] = useState<Record<string, number>>({})
  const [tracking, setTracking] = useState<Record<string, TrackingDraft>>({})
  const [serialLineId, setSerialLineId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const ordersQuery = useSalesOrders(companyId, 1, 100)
  const warehousesQuery = useFlatWarehouses(companyId, 1, 100, true)
  const itemsQuery = useItems(companyId, 1, MAX_PAGE_SIZE)
  const itemById = useMemo(
    () => new Map(itemsQuery.items.map((item) => [item.id, item])),
    [itemsQuery.items],
  )

  const deliverableOrders = useMemo(
    () => ordersQuery.items.filter((o) => o.status === 'Submitted' || o.status === 'PartiallyDelivered'),
    [ordersQuery.items],
  )
  const selectedOrder = useMemo(
    () => deliverableOrders.find((o) => o.id === salesOrderId) ?? null,
    [deliverableOrders, salesOrderId],
  )

  useEffect(() => {
    if (isOpen) {
      setSalesOrderId('')
      setWarehouseId('')
      setPostingDate(todayIso())
      setQuantities({})
      setTracking({})
      setSerialLineId(null)
      setError(null)
    }
  }, [isOpen])

  useEffect(() => {
    if (selectedOrder) {
      const initial: Record<string, number> = {}
      for (const line of selectedOrder.lines) {
        initial[line.id] = Math.max(0, line.quantity - line.deliveredQuantity)
      }
      setQuantities(initial)
    }
  }, [selectedOrder])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!salesOrderId || !warehouseId || !selectedOrder) {
      setError(t('deliveryForm.errorRequired', 'Order and warehouse are required.'))
      return
    }
    const lines = selectedOrder.lines
      .map((line) => ({
        salesOrderItemId: line.id,
        itemId: line.itemId,
        qty: quantities[line.id] ?? 0,
        tracking: tracking[line.id] ?? { batchId: '', serials: [] },
        flags: itemById.get(line.itemId),
      }))
      .filter((l) => l.qty > 0)
    if (lines.length === 0) {
      setError(t('deliveryForm.errorLines', 'Enter a quantity > 0 for at least one line.'))
      return
    }
    // Batch-and-serial Fase 3: tracked lines must name their batch and list exactly qty
    // serials. The server re-validates expiry, balances, warehouse and duplicates.
    for (const line of lines) {
      if (line.flags?.hasBatchNo && !line.tracking.batchId) {
        setError(t('deliveryForm.errorBatch', 'Batch-tracked lines need a batch selection.'))
        return
      }
      if (line.flags?.hasSerialNo && line.tracking.serials.length !== line.qty) {
        setError(
          t('deliveryForm.errorSerialCount', 'Serial-tracked lines need exactly one serial per unit.'),
        )
        return
      }
    }
    setIsSubmitting(true)
    try {
      await onSave({
        salesOrderId,
        warehouseId,
        postingDate,
        lines: lines.map((line) => ({
          salesOrderItemId: line.salesOrderItemId,
          itemId: line.itemId,
          qty: line.qty,
          ...(line.tracking.batchId ? { batchId: line.tracking.batchId } : {}),
          ...(line.tracking.serials.length > 0 ? { serialNumbers: line.tracking.serials } : {}),
        })),
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(translateErrorCode(i18n, err.code, err.message))
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError(t('deliveryForm.errorGeneric', 'An error occurred while posting.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('deliveryForm.titleNew', 'New Delivery Note')}</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('deliveryForm.salesOrder', 'Sales order *')}</label>
                <select
                  value={salesOrderId}
                  onChange={(e) => setSalesOrderId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  required
                >
                  <option value="">{t('form.selectNone', '-- Select --')}</option>
                  {deliverableOrders.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.orderNumber} — {o.customerName || o.customerCode}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('deliveryForm.warehouse', 'Warehouse *')}</label>
                <select
                  value={warehouseId}
                  onChange={(e) => setWarehouseId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  required
                >
                  <option value="">{t('form.selectNone', '-- Select --')}</option>
                  {warehousesQuery.items.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.code} — {w.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('form.postingDate', 'Posting date *')}</label>
                <Input type="date" value={postingDate} onChange={(e) => setPostingDate(e.target.value)} required />
              </div>
            </div>

            {selectedOrder && (
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('deliveryForm.linesPending', 'Lines (pending = ordered − delivered)')}</label>
                {selectedOrder.lines.map((line) => {
                  const pending = Math.max(0, line.quantity - line.deliveredQuantity)
                  const flags = itemById.get(line.itemId)
                  const lineTracking = tracking[line.id] ?? { batchId: '', serials: [] }
                  const setLineTracking = (patch: Partial<TrackingDraft>) =>
                    setTracking((prev) => ({ ...prev, [line.id]: { ...lineTracking, ...patch } }))
                  return (
                    <div key={line.id} className="space-y-1">
                      <div className="grid grid-cols-12 gap-2 items-center">
                        <span className="col-span-7 text-sm text-slate-700">
                          {line.itemCode} — {line.itemName}
                          <span className="ml-2 font-mono text-xs text-slate-500">{t('deliveryForm.pending', 'pending {{count}}', { count: pending })}</span>
                        </span>
                        <Input
                          type="number"
                          min={0}
                          max={pending}
                          step="0.0001"
                          value={quantities[line.id] ?? 0}
                          onChange={(e) =>
                            setQuantities((prev) => ({ ...prev, [line.id]: Number(e.target.value) }))
                          }
                          className="col-span-5"
                        />
                      </div>
                      {(flags?.hasBatchNo || flags?.hasSerialNo) && (
                        <div className="grid grid-cols-1 gap-2 rounded-lg bg-slate-50 px-2 py-2 sm:grid-cols-2">
                          {flags?.hasBatchNo && (
                            <BatchSelector
                              itemId={line.itemId}
                              warehouseId={warehouseId}
                              value={lineTracking.batchId}
                              onChange={(batchId) => setLineTracking({ batchId })}
                            />
                          )}
                          {flags?.hasSerialNo && (
                            <div className="flex items-center gap-2">
                              <button
                                type="button"
                                onClick={() => setSerialLineId(line.id)}
                                className="inline-flex items-center gap-1 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                              >
                                {t('deliveryForm.serials', 'Serials')}
                                <span
                                  className={`rounded-full px-2 py-0.5 font-mono text-[11px] font-semibold ${
                                    lineTracking.serials.length === (quantities[line.id] ?? 0) &&
                                    (quantities[line.id] ?? 0) > 0
                                      ? 'bg-emerald-100 text-emerald-800'
                                      : 'bg-amber-100 text-amber-800'
                                  }`}
                                >
                                  {lineTracking.serials.length}/{quantities[line.id] ?? 0}
                                </span>
                              </button>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            )}
            {serialLineId &&
              (() => {
                const orderLine = selectedOrder?.lines.find((l) => l.id === serialLineId) ?? null
                return (
                  <SerialNoPickerModal
                    isOpen={serialLineId !== null}
                    onClose={() => setSerialLineId(null)}
                    itemLabel={orderLine ? `${orderLine.itemCode} — ${orderLine.itemName}` : ''}
                    expectedCount={quantities[serialLineId] ?? 0}
                    initial={tracking[serialLineId]?.serials ?? []}
                    onApply={(serials) =>
                      setTracking((prev) => ({
                        ...prev,
                        [serialLineId]: { batchId: prev[serialLineId]?.batchId ?? '', serials },
                      }))
                    }
                  />
                )
              })()}
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('form.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('deliveryForm.posting', 'Posting…') : t('deliveryForm.post', 'Post')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
