import { Plus, Trash2, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { localISODate } from './format'
import { postStockEntry, type CreateStockEntryPayload } from './useStockData'
import type { FlatWarehouse, StockEntryPosting, StockEntryType } from './types'

interface LineDraft {
  key: string
  itemId: string
  qty: string
  rate: string
}

interface StockEntryModalProps {
  companyId: string
  items: { id: string; code: string; name: string; isActive: boolean }[]
  /** Posting targets: leaf warehouses only (group nodes hold no stock). */
  leafWarehouses: FlatWarehouse[]
  defaultItemId: string | null
  onClose: () => void
  /** Hand the posting to the container so it can refresh the stock levels. */
  onPosted: (posting: StockEntryPosting) => void
}

const ENTRY_TYPE_LABEL: Record<StockEntryType, string> = {
  MaterialReceipt: 'Material Receipt',
  MaterialIssue: 'Material Issue',
  MaterialTransfer: 'Material Transfer',
}

/** What the voucher posts, spelled out so the user knows which scenario they are running. */
const ENTRY_TYPE_HINT: Record<StockEntryType, string> = {
  MaterialReceipt:
    'Receiving goods: Dr Stock In Hand (1310) / Cr Stock Received But Not Billed (2120), FIFO layer valued at the unit rate you enter.',
  MaterialIssue:
    'Consuming goods: FIFO layers flow to COGS (Dr 5210 / Cr 1310). Rejected when the warehouse cannot cover the quantity.',
  MaterialTransfer:
    'Moving between warehouses: Kardex rows only, no General Ledger impact.',
}

let lineSequence = 0
const nextLineKey = (): string => `line-${++lineSequence}`

/**
 * Stock voucher composer (Task 3.4). Posting is immediate - there is no draft lifecycle - so the
 * button says "Post", the modal closes on success and the container reloads the item list.
 *
 * One `Idempotency-Key` per modal instance: it is minted here and kept for every retry of this
 * form, so a duplicate submission replays the original 201 instead of posting the voucher twice.
 * A failed request releases the key server-side, so correcting the form and retrying is safe.
 */
export function StockEntryModal({
  companyId,
  items,
  leafWarehouses,
  defaultItemId,
  onClose,
  onPosted,
}: StockEntryModalProps) {
  const activeItems = items.filter((item) => item.isActive)
  const [entryType, setEntryType] = useState<StockEntryType>('MaterialReceipt')
  const [warehouseId, setWarehouseId] = useState(leafWarehouses[0]?.id ?? '')
  const [targetWarehouseId, setTargetWarehouseId] = useState('')
  const [postingDate, setPostingDate] = useState(localISODate)
  const [lines, setLines] = useState<LineDraft[]>(() => [
    { key: nextLineKey(), itemId: defaultItemId ?? activeItems[0]?.id ?? '', qty: '', rate: '' },
  ])
  const [error, setError] = useState<ApiError | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [idempotencyKey] = useState(() => crypto.randomUUID())

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  const requiresRate = entryType === 'MaterialReceipt'
  const isTransfer = entryType === 'MaterialTransfer'
  const needsTarget = isTransfer

  const updateLine = (key: string, patch: Partial<LineDraft>): void =>
    setLines((current) => current.map((line) => (line.key === key ? { ...line, ...patch } : line)))

  const validate = (): string | null => {
    if (leafWarehouses.length === 0) return 'This company has no warehouses to post into.'
    if (activeItems.length === 0) return 'This company has no active items to post.'
    if (!warehouseId) return 'Select a warehouse.'
    if (needsTarget) {
      if (!targetWarehouseId) return 'Select a target warehouse for the transfer.'
      if (targetWarehouseId === warehouseId) return 'Target warehouse must differ from the source.'
    }
    if (lines.length === 0) return 'Add at least one line.'
    for (const line of lines) {
      if (!line.itemId) return 'Every line needs an item.'
      const qty = Number(line.qty)
      if (!Number.isFinite(qty) || qty <= 0) return 'Quantities must be greater than zero.'
      if (requiresRate) {
        const rate = Number(line.rate)
        if (!Number.isFinite(rate) || rate <= 0) {
          return 'Receipts need a unit rate: it opens the FIFO layer that later issues consume.'
        }
      }
    }
    return null
  }

  const handlePost = async (): Promise<void> => {
    const problem = validate()
    if (problem) {
      setError(new ApiError(400, 'Stock entry is incomplete', problem))
      return
    }

    setSubmitting(true)
    setError(null)
    try {
      const payload: CreateStockEntryPayload = {
        companyId,
        entryType,
        warehouseId,
        ...(needsTarget ? { targetWarehouseId } : {}),
        postingDate,
        lines: lines.map((line) => ({
          itemId: line.itemId,
          qty: Number(line.qty),
          ...(requiresRate ? { rate: Number(line.rate) } : {}),
        })),
      }
      onPosted(await postStockEntry(payload, idempotencyKey))
    } catch (cause) {
      setError(
        cause instanceof ApiError
          ? cause
          : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause)),
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/40 p-4 pt-12"
      role="presentation"
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="stock-entry-title"
        className="w-full max-w-2xl rounded-xl bg-white shadow-xl"
      >
        <div className="flex items-start justify-between border-b border-slate-200 px-6 py-4">
          <div>
            <h3 id="stock-entry-title" className="text-base font-semibold text-slate-900">
              New stock entry
            </h3>
            <p className="mt-0.5 text-xs text-slate-500">{ENTRY_TYPE_HINT[entryType]}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="size-4" aria-hidden="true" />
          </button>
        </div>

        <div className="space-y-4 px-6 py-4">
          <fieldset className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <legend className="sr-only">Voucher header</legend>

            <label className="block text-xs font-medium text-slate-600">
              Type
              <select
                value={entryType}
                onChange={(event) => setEntryType(event.target.value as StockEntryType)}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
              >
                <option value="MaterialReceipt">Material Receipt</option>
                <option value="MaterialIssue">Material Issue</option>
                <option value="MaterialTransfer">Material Transfer</option>
              </select>
            </label>

            <label className="block text-xs font-medium text-slate-600">
              {isTransfer ? 'From warehouse' : 'Warehouse'}
              <select
                value={warehouseId}
                onChange={(event) => setWarehouseId(event.target.value)}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
              >
                <option value="">Select…</option>
                {leafWarehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>
                    {warehouse.code} — {warehouse.name}
                  </option>
                ))}
              </select>
            </label>

            {needsTarget ? (
              <label className="block text-xs font-medium text-slate-600">
                To warehouse
                <select
                  value={targetWarehouseId}
                  onChange={(event) => setTargetWarehouseId(event.target.value)}
                  className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
                >
                  <option value="">Select…</option>
                  {leafWarehouses
                    .filter((warehouse) => warehouse.id !== warehouseId)
                    .map((warehouse) => (
                      <option key={warehouse.id} value={warehouse.id}>
                        {warehouse.code} — {warehouse.name}
                      </option>
                    ))}
                </select>
              </label>
            ) : (
              <label className="block text-xs font-medium text-slate-600">
                Posting date
                <input
                  type="date"
                  value={postingDate}
                  onChange={(event) => setPostingDate(event.target.value)}
                  className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
                />
              </label>
            )}
          </fieldset>

          <div>
            <div className="mb-2 flex items-center justify-between">
              <span className="text-xs font-medium text-slate-600">Lines</span>
              <button
                type="button"
                onClick={() =>
                  setLines((current) => [
                    ...current,
                    { key: nextLineKey(), itemId: activeItems[0]?.id ?? '', qty: '', rate: '' },
                  ])
                }
                className="inline-flex items-center gap-1 rounded border border-slate-200 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50"
              >
                <Plus className="size-3" aria-hidden="true" />
                Add line
              </button>
            </div>

            <div className="space-y-2">
              {lines.map((line) => (
                <div
                  key={line.key}
                  className="grid grid-cols-[1fr_5.5rem_5.5rem_2rem] items-center gap-2"
                >
                  <select
                    value={line.itemId}
                    onChange={(event) => updateLine(line.key, { itemId: event.target.value })}
                    aria-label="Item"
                    className="rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
                  >
                    <option value="">Select item…</option>
                    {activeItems.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code} — {item.name}
                      </option>
                    ))}
                  </select>
                  <input
                    type="number"
                    min="0"
                    step="any"
                    inputMode="decimal"
                    value={line.qty}
                    onChange={(event) => updateLine(line.key, { qty: event.target.value })}
                    aria-label="Quantity"
                    placeholder="Qty"
                    className="rounded border border-slate-300 px-2 py-1.5 text-right text-sm text-slate-900"
                  />
                  {requiresRate ? (
                    <input
                      type="number"
                      min="0"
                      step="any"
                      inputMode="decimal"
                      value={line.rate}
                      onChange={(event) => updateLine(line.key, { rate: event.target.value })}
                      aria-label="Unit rate"
                      placeholder="Rate"
                      className="rounded border border-slate-300 px-2 py-1.5 text-right text-sm text-slate-900"
                    />
                  ) : (
                    <span className="text-right text-xs text-slate-400" title="FIFO valued">
                      —
                    </span>
                  )}
                  <button
                    type="button"
                    onClick={() => setLines((current) => current.filter((l) => l.key !== line.key))}
                    aria-label="Remove line"
                    disabled={lines.length === 1}
                    className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    <Trash2 className="size-4" aria-hidden="true" />
                  </button>
                </div>
              ))}
            </div>
          </div>

          {error ? (
            <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              <p className="font-medium">
                Posting rejected{error.code ? ` · ${error.code}` : ''}
                {error.status ? ` (HTTP ${error.status})` : ''}
              </p>
              <p className="mt-1">{error.message}</p>
            </div>
          ) : null}
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-slate-200 px-6 py-4">
          <span className="text-xs text-slate-400">
            Posts atomically: Kardex rows + balanced General Ledger lines.
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={() => {
                void handlePost()
              }}
              disabled={submitting}
              className="rounded bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {submitting ? 'Posting…' : `Post ${ENTRY_TYPE_LABEL[entryType]}`}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
