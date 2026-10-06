import { Plus, Trash2, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import { MAX_PAGE_SIZE } from '../../lib/pagination'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { localISODate } from './format'
import { postStockEntry, useItems, type CreateStockEntryPayload } from './useStockData'
import type { FlatWarehouse, StockEntryPosting, StockEntryType } from './types'

interface LineDraft {
  key: string
  itemId: string
  qty: string
  rate: string
}

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
            <legend className="sr-only">Voucher header</legend>

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
                    { key: nextLineKey(), itemId: activeItems[0]?.id ?? '', qty: '', rate: '' },
                  ])
                }
                className="inline-flex items-center gap-1 rounded border border-slate-200 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50"
              >
                <Plus className="size-3" aria-hidden="true" />
                {t('entry.addLine')}
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
              ))}
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
    </div>
  )
}
