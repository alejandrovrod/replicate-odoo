import { Plus, Trash2, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import { MAX_PAGE_SIZE } from '../../lib/pagination'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { localISODate } from './format'
import { postStockEntry, useItems, type CreateStockEntryPayload } from './useStockData'
import type { FlatWarehouse, StockEntryPosting, StockEntryType } from './types'
import { BatchSelector } from './components/BatchSelector'
import { SerialNoPickerModal } from './components/SerialNoPickerModal'

interface LineDraft {
  key: string
  itemId: string
  qty: string
  rate: string
  /** Batch-and-serial Fase 3: selected batch id (issues/transfers). */
  batchId: string
  /** Batch-and-serial Fase 3: receipt batch number (+ master data when creating). */
  batchNo: string
  manufacturingDate: string
  expiryDate: string
  /** Batch-and-serial Fase 3: exact serials moved (count must equal qty). */
  serials: string[]
}

const emptyTracking = { batchId: '', batchNo: '', manufacturingDate: '', expiryDate: '', serials: [] as string[] }

interface StockEntryModalProps {
  companyId: string
  /** Posting targets: leaf warehouses only (group nodes hold no stock). */
  leafWarehouses: FlatWarehouse[]
  defaultItemId: string | null
  onClose: () => void
  /** Hand the posting to the container so it can refresh the stock levels. */
  onPosted: (posting: StockEntryPosting) => void
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
 *
 * Voucher-type labels come from the `stock` namespace (`entryType.*`, `entryTypeHint.*`): the
 * enum values themselves (`MaterialReceipt`, …) are the wire contract and are never translated.
 */
export function StockEntryModal({
  companyId,
  leafWarehouses,
  defaultItemId,
  onClose,
  onPosted,
}: StockEntryModalProps) {
  // Item picker: the table page must not limit what can be posted, so the modal reads its own
  // bounded catalog page (MAX_PAGE_SIZE, no pager) — the documented picker pattern.
  const catalogQuery = useItems(companyId, 1, MAX_PAGE_SIZE)
  const catalogReady = catalogQuery.status === 'success'
  const activeItems = (catalogReady ? catalogQuery.items : []).filter((item) => item.isActive)
  const { t, i18n } = useTranslation('stock')
  const [entryType, setEntryType] = useState<StockEntryType>('MaterialReceipt')
  const [warehouseId, setWarehouseId] = useState(leafWarehouses[0]?.id ?? '')
  const [targetWarehouseId, setTargetWarehouseId] = useState('')
  const [postingDate, setPostingDate] = useState(localISODate)
  const [lines, setLines] = useState<LineDraft[]>(() => [
    { key: nextLineKey(), itemId: defaultItemId ?? activeItems[0]?.id ?? '', qty: '', rate: '', ...emptyTracking },
  ])
  const [serialLineKey, setSerialLineKey] = useState<string | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [idempotencyKey] = useState(() => crypto.randomUUID())

  const itemById = useMemo(() => new Map(activeItems.map((item) => [item.id, item])), [activeItems])
  const serialLine = serialLineKey ? (lines.find((l) => l.key === serialLineKey) ?? null) : null

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

  // Client-side validation messages are translated at submit time: they are transient (the
  // error clears on the next submit or close), while server rejections keep the raw ApiError
  // and translate at render through `translateErrorCode`, so they follow language switches.
  const validate = (): string | null => {
    if (leafWarehouses.length === 0) return t('validation.noWarehouses')
    if (activeItems.length === 0) return t('validation.noActiveItems')
    if (!warehouseId) return t('validation.selectWarehouse')
    if (needsTarget) {
      if (!targetWarehouseId) return t('validation.selectTarget')
      if (targetWarehouseId === warehouseId) return t('validation.targetDiffers')
    }
    if (lines.length === 0) return t('validation.addLine')
    const isReceipt = entryType === 'MaterialReceipt'
    for (const line of lines) {
      if (!line.itemId) return t('validation.lineNeedsItem')
      const qty = Number(line.qty)
      if (!Number.isFinite(qty) || qty <= 0) return t('validation.qtyPositive')
      if (requiresRate) {
        const rate = Number(line.rate)
        if (!Number.isFinite(rate) || rate <= 0) {
          return t('validation.rateRequired')
        }
      }
      // Batch-and-serial Fase 3: tracked items must name their batch (number on receipts,
      // id on issues/transfers) and list exactly qty serials. The server re-validates
      // expiry, balances, warehouse and duplicates authoritatively.
      const item = itemById.get(line.itemId)
      if (item?.hasBatchNo) {
        if (isReceipt) {
          if (!line.batchNo.trim()) return t('tracking.validationBatchNo', 'Batch-tracked lines need a batch number.')
        } else if (!line.batchId) {
          return t('tracking.validationBatchId', 'Batch-tracked lines need a batch selection.')
        }
      }
      if (item?.hasSerialNo) {
        if (line.serials.length !== qty) {
          return t('tracking.validationSerialCount', 'Serial-tracked lines need exactly one serial per unit ({{qty}}).', {
            qty,
          })
        }
      }
    }
    return null
  }

  const handlePost = async (): Promise<void> => {
    const problem = validate()
    if (problem) {
      setError(new ApiError(400, t('validation.incomplete'), problem))
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
          // Batch-and-serial Fase 3: only tracked lines carry these (the server ignores
          // them for untracked items, but omitting keeps the payload honest).
          ...(line.batchId ? { batchId: line.batchId } : {}),
          ...(line.batchNo.trim() ? { batchNo: line.batchNo.trim() } : {}),
          ...(line.manufacturingDate ? { manufacturingDate: line.manufacturingDate } : {}),
          ...(line.expiryDate ? { expiryDate: line.expiryDate } : {}),
          ...(line.serials.length > 0 ? { serialNumbers: line.serials } : {}),
        })),
      }
      onPosted(await postStockEntry(payload, idempotencyKey))
    } catch (cause) {
      setError(
        cause instanceof ApiError
          ? cause
          : new ApiError(0, t('entry.unexpectedTitle'), cause instanceof Error ? cause.message : String(cause)),
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
              {t('entry.title')}
            </h3>
            <p className="mt-0.5 text-xs text-slate-500">{t(`entryTypeHint.${entryType}`)}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('entry.close')}
            className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="size-4" aria-hidden="true" />
          </button>
        </div>

        <div className="space-y-4 px-6 py-4">
          <fieldset className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <legend className="sr-only">{t('entry.voucherHeader', 'Voucher header')}</legend>

            <label className="block text-xs font-medium text-slate-600">
              {t('entry.type')}
              <select
                value={entryType}
                onChange={(event) => setEntryType(event.target.value as StockEntryType)}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
              >
                <option value="MaterialReceipt">{t('entryType.MaterialReceipt')}</option>
                <option value="MaterialIssue">{t('entryType.MaterialIssue')}</option>
                <option value="MaterialTransfer">{t('entryType.MaterialTransfer')}</option>
              </select>
            </label>

            <label className="block text-xs font-medium text-slate-600">
              {isTransfer ? t('entry.fromWarehouse') : t('entry.warehouse')}
              <select
                value={warehouseId}
                onChange={(event) => setWarehouseId(event.target.value)}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
              >
                <option value="">{t('entry.selectWarehouse')}</option>
                {leafWarehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>
                    {warehouse.code} — {warehouse.name}
                  </option>
                ))}
              </select>
            </label>

            {needsTarget ? (
              <label className="block text-xs font-medium text-slate-600">
                {t('entry.toWarehouse')}
                <select
                  value={targetWarehouseId}
                  onChange={(event) => setTargetWarehouseId(event.target.value)}
                  className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
                >
                  <option value="">{t('entry.selectWarehouse')}</option>
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
                {t('entry.postingDate')}
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
              <span className="text-xs font-medium text-slate-600">{t('entry.lines')}</span>
              <button
                type="button"
                onClick={() =>
                  setLines((current) => [
                    ...current,
                    { key: nextLineKey(), itemId: activeItems[0]?.id ?? '', qty: '', rate: '', ...emptyTracking },
                  ])
                }
                className="inline-flex items-center gap-1 rounded border border-slate-200 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50"
              >
                <Plus className="size-3" aria-hidden="true" />
                {t('entry.addLine')}
              </button>
            </div>

            <div className="space-y-2">
              {lines.map((line) => {
                const item = itemById.get(line.itemId)
                const trackedBatch = Boolean(item?.hasBatchNo)
                const trackedSerial = Boolean(item?.hasSerialNo)
                return (
                <div key={line.key} className="space-y-1 rounded-lg border border-transparent">
                  <div className="grid grid-cols-[1fr_5.5rem_5.5rem_2rem] items-center gap-2">
                  <select
                    value={line.itemId}
                    onChange={(event) =>
                      // Switching items resets tracking: batch/serials belong to one item only.
                      updateLine(line.key, { itemId: event.target.value, ...emptyTracking })
                    }
                    aria-label={t('entry.item')}
                    className="rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
                  >
                    <option value="">{t('entry.selectItem')}</option>
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
                    aria-label={t('entry.qty')}
                    placeholder={t('entry.qty')}
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
                      aria-label={t('entry.rate')}
                      placeholder={t('entry.rate')}
                      className="rounded border border-slate-300 px-2 py-1.5 text-right text-sm text-slate-900"
                    />
                  ) : (
                    <span className="text-right text-xs text-slate-400" title={t('entry.fifoValued')}>
                      —
                    </span>
                  )}
                  <button
                    type="button"
                    onClick={() => setLines((current) => current.filter((l) => l.key !== line.key))}
                    aria-label={t('entry.removeLine')}
                    disabled={lines.length === 1}
                    className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    <Trash2 className="size-4" aria-hidden="true" />
                  </button>
                  </div>
                  {(trackedBatch || trackedSerial) && line.itemId && (
                    <div className="grid grid-cols-1 gap-2 rounded-lg bg-slate-50 px-2 py-2 sm:grid-cols-2">
                      {trackedBatch &&
                        (entryType === 'MaterialReceipt' ? (
                          <div className="flex gap-2">
                            <input
                              value={line.batchNo}
                              onChange={(event) => updateLine(line.key, { batchNo: event.target.value, batchId: '' })}
                              aria-label={t('tracking.batchNo', 'Batch number')}
                              placeholder={t('tracking.batchNo', 'Batch number')}
                              className="min-w-0 flex-1 rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
                            />
                            <input
                              type="date"
                              value={line.expiryDate}
                              onChange={(event) => updateLine(line.key, { expiryDate: event.target.value })}
                              aria-label={t('tracking.expiryDate', 'Expiry date')}
                              title={t('tracking.expiryDate', 'Expiry date')}
                              className="w-36 rounded border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
                            />
                          </div>
                        ) : (
                          <BatchSelector
                            itemId={line.itemId}
                            warehouseId={warehouseId}
                            value={line.batchId}
                            onChange={(batchId) => updateLine(line.key, { batchId })}
                          />
                        ))}
                      {trackedSerial && (
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() => setSerialLineKey(line.key)}
                            className="inline-flex items-center gap-1 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                          >
                            {t('tracking.serialButton', 'Serials')}
                            <span
                              className={`rounded-full px-2 py-0.5 font-mono text-[11px] font-semibold ${
                                line.serials.length === Number(line.qty) && Number(line.qty) > 0
                                  ? 'bg-emerald-100 text-emerald-800'
                                  : 'bg-amber-100 text-amber-800'
                              }`}
                            >
                              {line.serials.length}/{Number(line.qty) || 0}
                            </span>
                          </button>
                          {line.serials.length > 0 && (
                            <span className="truncate font-mono text-[11px] text-slate-500" title={line.serials.join(', ')}>
                              {line.serials.slice(0, 2).join(', ')}
                              {line.serials.length > 2 ? ` +${line.serials.length - 2}` : ''}
                            </span>
                          )}
                        </div>
                      )}
                    </div>
                  )}
                </div>
                )
              })}
            </div>
          </div>

          {error ? (
            <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              <p className="font-medium">
                {t('entry.rejected')}
                {error.code ? ` · ${error.code}` : ''}
                {error.status ? ` (HTTP ${error.status})` : ''}
              </p>
              <p className="mt-1">{translateErrorCode(i18n, error.code, error.message)}</p>
            </div>
          ) : null}
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-slate-200 px-6 py-4">
          <span className="text-xs text-slate-400">{t('entry.atomicHint')}</span>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              {t('entry.cancel')}
            </button>
            <button
              type="button"
              onClick={() => {
                void handlePost()
              }}
              disabled={submitting || !catalogReady}
              className="rounded bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {submitting
                ? t('entry.posting')
                : t('entry.post', { type: t(`entryType.${entryType}`) })}
            </button>
          </div>
        </div>
      </div>

      {serialLine && (
        <SerialNoPickerModal
          isOpen={serialLineKey !== null}
          onClose={() => setSerialLineKey(null)}
          itemLabel={itemById.get(serialLine.itemId)?.code ?? serialLine.itemId}
          expectedCount={Number(serialLine.qty) || 0}
          initial={serialLine.serials}
          onApply={(serials) => updateLine(serialLine.key, { serials })}
        />
      )}
    </div>
  )
}
