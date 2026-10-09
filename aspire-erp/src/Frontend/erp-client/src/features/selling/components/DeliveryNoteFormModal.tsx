import { useEffect, useMemo, useState } from 'react'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { useSalesOrders } from '../api/useSalesOrders'
import { useFlatWarehouses } from '../../stock/useStockData'

interface DeliveryNoteFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: {
    salesOrderId: string
    warehouseId: string
    postingDate: string
    lines: { salesOrderItemId: string; itemId: string; qty: number }[]
  }) => Promise<void>
  companyId: string
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export function DeliveryNoteFormModal({ isOpen, onClose, onSave, companyId }: DeliveryNoteFormModalProps) {
  const [salesOrderId, setSalesOrderId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [postingDate, setPostingDate] = useState(todayIso())
  const [quantities, setQuantities] = useState<Record<string, number>>({})
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const ordersQuery = useSalesOrders(companyId, 1, 100)
  const warehousesQuery = useFlatWarehouses(companyId, 1, 100, true)

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
      setError('Order and warehouse are required.')
      return
    }
    const lines = selectedOrder.lines
      .map((line) => ({
        salesOrderItemId: line.id,
        itemId: line.itemId,
        qty: quantities[line.id] ?? 0,
      }))
      .filter((l) => l.qty > 0)
    if (lines.length === 0) {
      setError('Enter a quantity > 0 for at least one line.')
      return
    }
    setIsSubmitting(true)
    try {
      await onSave({ salesOrderId, warehouseId, postingDate, lines })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError('An error occurred while posting.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>New Delivery Note</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">Sales order *</label>
                <select
                  value={salesOrderId}
                  onChange={(e) => setSalesOrderId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  required
                >
                  <option value="">-- Select --</option>
                  {deliverableOrders.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.orderNumber} — {o.customerName || o.customerCode}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Warehouse *</label>
                <select
                  value={warehouseId}
                  onChange={(e) => setWarehouseId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  required
                >
                  <option value="">-- Select --</option>
                  {warehousesQuery.items.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.code} — {w.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Posting date *</label>
                <Input type="date" value={postingDate} onChange={(e) => setPostingDate(e.target.value)} required />
              </div>
            </div>

            {selectedOrder && (
              <div className="space-y-2">
                <label className="text-sm font-medium">Lines (pending = ordered − delivered)</label>
                {selectedOrder.lines.map((line) => {
                  const pending = Math.max(0, line.quantity - line.deliveredQuantity)
                  return (
                    <div key={line.id} className="grid grid-cols-12 gap-2 items-center">
                      <span className="col-span-7 text-sm text-slate-700">
                        {line.itemCode} — {line.itemName}
                        <span className="ml-2 font-mono text-xs text-slate-500">pending {pending.toLocaleString()}</span>
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
                  )
                })}
              </div>
            )}
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Posting…' : 'Post'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
