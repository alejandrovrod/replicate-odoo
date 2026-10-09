import { useEffect, useState } from 'react'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { useCustomers } from '../api/useCustomers'
import { useItems } from '../../stock/useStockData'

interface LineDraft {
  itemId: string
  quantity: number
  rate: number
}

interface SalesOrderFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: {
    customerId: string
    transactionDate: string
    deliveryDate: string
    lines: LineDraft[]
  }) => Promise<void>
  companyId: string
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export function SalesOrderFormModal({ isOpen, onClose, onSave, companyId }: SalesOrderFormModalProps) {
  const [customerId, setCustomerId] = useState('')
  const [transactionDate, setTransactionDate] = useState(todayIso())
  const [deliveryDate, setDeliveryDate] = useState(todayIso())
  const [lines, setLines] = useState<LineDraft[]>([{ itemId: '', quantity: 1, rate: 0 }])
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const customersQuery = useCustomers(companyId, 1, 100)
  const itemsQuery = useItems(companyId, 1, 100)

  useEffect(() => {
    if (isOpen) {
      setCustomerId('')
      setTransactionDate(todayIso())
      setDeliveryDate(todayIso())
      setLines([{ itemId: '', quantity: 1, rate: 0 }])
      setError(null)
    }
  }, [isOpen])

  const updateLine = (index: number, patch: Partial<LineDraft>) => {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!customerId) {
      setError('Customer is required.')
      return
    }
    const validLines = lines.filter((l) => l.itemId && l.quantity > 0)
    if (validLines.length === 0) {
      setError('Add at least one line with item and quantity > 0.')
      return
    }
    setIsSubmitting(true)
    try {
      await onSave({ customerId, transactionDate, deliveryDate, lines: validLines })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError('An error occurred while saving.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>New Sales Order</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">Customer *</label>
                <select
                  value={customerId}
                  onChange={(e) => setCustomerId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  required
                >
                  <option value="">-- Select --</option>
                  {customersQuery.items.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Order date *</label>
                <Input type="date" value={transactionDate} onChange={(e) => setTransactionDate(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Delivery date *</label>
                <Input type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} required />
              </div>
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">Lines</label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setLines((prev) => [...prev, { itemId: '', quantity: 1, rate: 0 }])}
                >
                  Add line
                </Button>
              </div>
              {lines.map((line, i) => (
                <div key={i} className="grid grid-cols-12 gap-2">
                  <select
                    value={line.itemId}
                    onChange={(e) => updateLine(i, { itemId: e.target.value })}
                    className="col-span-6 flex h-10 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  >
                    <option value="">-- Item --</option>
                    {itemsQuery.items.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code} — {item.name}
                      </option>
                    ))}
                  </select>
                  <Input
                    type="number"
                    min={0}
                    step="0.0001"
                    value={line.quantity}
                    onChange={(e) => updateLine(i, { quantity: Number(e.target.value) })}
                    placeholder="Qty"
                    className="col-span-3"
                  />
                  <Input
                    type="number"
                    min={0}
                    step="0.0001"
                    value={line.rate}
                    onChange={(e) => updateLine(i, { rate: Number(e.target.value) })}
                    placeholder="Rate"
                    className="col-span-3"
                  />
                </div>
              ))}
            </div>
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
