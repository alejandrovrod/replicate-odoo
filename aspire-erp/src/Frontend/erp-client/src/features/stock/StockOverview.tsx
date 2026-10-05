import { Boxes, CheckCircle2, Layers, Plus, Warehouse, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useTenantStore } from '../../store/useTenantStore'
import { formatMoney, formatQty } from './format'
import { ItemList } from './ItemList'
import { StockEntryModal } from './StockEntryModal'
import { flattenWarehouses, type StockEntryPosting, type StockEntryType } from './types'
import { useItems, useStockEntries, useWarehouses } from './useStockData'

const ENTRY_TYPE_BADGE: Record<StockEntryType, string> = {
  MaterialReceipt: 'bg-emerald-100 text-emerald-700',
  MaterialIssue: 'bg-rose-100 text-rose-700',
  MaterialTransfer: 'bg-sky-100 text-sky-700',
}

/**
 * Stock & Inventory page (Task 3.4 container). It owns the three queries, composes the
 * presentational `ItemList` and mounts `StockEntryModal`; after a successful post it reloads
 * both queries, which is what makes the updated stock levels appear in real time.
 *
 * The warehouse/movement sections below kept the original overview layout but now read the real
 * endpoints instead of static fixtures.
 */
export function StockOverview() {
  const { t } = useTranslation('stock')
  const companyId = useTenantStore((state) => state.companyId)
  const tenantId = useTenantStore((state) => state.tenantId)
  const itemsQuery = useItems(companyId)
  const warehousesQuery = useWarehouses(companyId)
  const entriesQuery = useStockEntries(companyId)

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null)
  const [lastPosted, setLastPosted] = useState<StockEntryPosting | null>(null)

  const items = itemsQuery.data
  const leafWarehouses = useMemo(
    () => flattenWarehouses(warehousesQuery.data, true),
    [warehousesQuery.data],
  )

  const totalValue = items.reduce(
    (sum, item) => sum + item.stock.reduce((lineSum, row) => lineSum + row.value, 0),
    0,
  )
  const activeSkus = items.filter((item) => item.isActive).length

  const stockedSkus = (warehouseId: string): number =>
    items.filter((item) =>
      item.stock.some((row) => row.warehouseId === warehouseId && row.qty !== 0),
    ).length

  const warehouseValue = (warehouseId: string): number =>
    items.reduce(
      (sum, item) => sum + (item.stock.find((row) => row.warehouseId === warehouseId)?.value ?? 0),
      0,
    )

  const movements = entriesQuery.data
    .flatMap((entry) =>
      entry.lines.map((line) => ({ entry, line, key: `${entry.id}:${line.lineNumber}` })),
    )
    .sort((a, b) => b.entry.createdAt.localeCompare(a.entry.createdAt))

  const openModal = (itemId: string | null): void => {
    setSelectedItemId(itemId)
    setIsModalOpen(true)
  }

  const handlePosted = (posting: StockEntryPosting): void => {
    setIsModalOpen(false)
    setSelectedItemId(null)
    setLastPosted(posting)
    itemsQuery.reload()
    entriesQuery.reload()
  }

  if (!companyId || !tenantId) {
    return (
      <p className="rounded-md border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        {t('overview.missingTenant')}
      </p>
    )
  }

  if (itemsQuery.status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          {t('overview.loadFailed')}
          {itemsQuery.error?.status ? ` (HTTP ${itemsQuery.error.status})` : ''}.
        </p>
        {itemsQuery.error?.message ? (
          <p className="mt-1">{itemsQuery.error.message}</p>
        ) : null}
        <button
          type="button"
          onClick={() => {
            itemsQuery.reload()
          }}
          className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
        >
          {t('overview.retry')}
        </button>
      </div>
    )
  }

  if (itemsQuery.status !== 'success') {
    return <p className="px-1 py-3 text-sm text-slate-500">{t('overview.loading')}</p>
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-slate-900">{t('overview.title')}</h2>
          <p className="text-xs text-slate-500">{t('overview.subtitle')}</p>
        </div>
        <button
          type="button"
          onClick={() => openModal(null)}
          className="inline-flex items-center gap-1.5 rounded bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          <Plus className="size-4" aria-hidden="true" />
          {t('overview.newEntry')}
        </button>
      </div>

      {/* Post confirmation: proves the double-entry balance and that the reload happened */}
      {lastPosted ? (
        <div className="flex items-start justify-between gap-3 rounded-md border border-emerald-300 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
          <p className="flex items-start gap-2">
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>
              {t('posted.summary', {
                voucherNo: lastPosted.entry.voucherNo,
                debit: formatMoney(lastPosted.totalDebit),
                credit: formatMoney(lastPosted.totalCredit),
                glLines: lastPosted.glEntries.length,
                kardexRows: lastPosted.ledgerEntries.length,
              })}
            </span>
          </p>
          <button
            type="button"
            onClick={() => setLastPosted(null)}
            aria-label={t('posted.dismiss')}
            className="rounded p-1 text-emerald-700 hover:bg-emerald-100"
          >
            <X className="size-4" aria-hidden="true" />
          </button>
        </div>
      ) : null}

      {/* Top stats */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
          <div className="flex items-center gap-2 text-slate-500">
            <Warehouse className="h-4 w-4 text-amber-600" />
            <span className="text-xs font-medium">{t('stats.warehouses')}</span>
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900">
            {t('stats.locations', { count: leafWarehouses.length })}
          </p>
          <span className="text-xs text-slate-400">{t('stats.locationsHint')}</span>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
          <div className="flex items-center gap-2 text-slate-500">
            <Boxes className="h-4 w-4 text-sky-600" />
            <span className="text-xs font-medium">{t('stats.activeSkus')}</span>
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900">
            {t('stats.activeItems', { count: activeSkus })}
          </p>
          <span className="text-xs text-slate-400">{t('stats.uniqueSkuHint')}</span>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
          <div className="flex items-center gap-2 text-slate-500">
            <Layers className="h-4 w-4 text-emerald-600" />
            <span className="text-xs font-medium">{t('stats.valuation')}</span>
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900">{formatMoney(totalValue)}</p>
          <span className="text-xs text-slate-400">{t('stats.fifoHint')}</span>
        </div>
      </div>

      {/* Warehouse grid */}
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-semibold text-slate-900">{t('warehouses.title')}</h3>
        <p className="mb-4 text-xs text-slate-500">{t('warehouses.subtitle')}</p>
        {leafWarehouses.length === 0 ? (
          <p className="text-sm text-slate-500">{t('warehouses.empty')}</p>
        ) : (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            {leafWarehouses.map((warehouse) => (
              <div key={warehouse.id} className="rounded-lg border border-slate-100 bg-slate-50 p-4">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-semibold text-slate-900">{warehouse.name}</span>
                  <span className="rounded border border-slate-200 bg-white px-2 py-0.5 font-mono text-[10px] text-slate-600">
                    {warehouse.code}
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-400">
                  {t('warehouses.statusLine', {
                    status: warehouse.isActive
                      ? t('warehouses.active')
                      : t('warehouses.inactive'),
                    account: warehouse.stockAccountId.slice(-4),
                  })}
                </p>
                <div className="mt-4 flex items-baseline justify-between border-t border-slate-200 pt-3 text-xs">
                  <span className="text-slate-500">
                    {t('warehouses.stockedSkus', { count: stockedSkus(warehouse.id) })}
                  </span>
                  <span className="font-bold text-slate-900">
                    {formatMoney(warehouseValue(warehouse.id))}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Item list (Task 3.4 core deliverable) */}
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h3 className="text-base font-semibold text-slate-900">{t('items.title')}</h3>
            <p className="text-xs text-slate-500">{t('items.subtitle')}</p>
          </div>
          <button
            type="button"
            onClick={() => openModal(null)}
            className="inline-flex items-center gap-1 rounded border border-slate-300 px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            <Plus className="size-3" aria-hidden="true" />
            {t('overview.newEntry')}
          </button>
        </div>
        <ItemList
          items={items}
          warehouses={warehousesQuery.data}
          status={itemsQuery.status}
          error={itemsQuery.error}
          onReload={itemsQuery.reload}
          onNewEntry={openModal}
        />
      </div>

      {/* Recent movements */}
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-semibold text-slate-900">{t('movements.title')}</h3>
        <p className="text-xs text-slate-500">{t('movements.subtitle')}</p>

        <div className="mt-4 overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">{t('movements.date')}</th>
                <th className="py-2.5 font-semibold">{t('movements.voucher')}</th>
                <th className="py-2.5 font-semibold">{t('movements.type')}</th>
                <th className="py-2.5 font-semibold">{t('movements.item')}</th>
                <th className="py-2.5 font-semibold text-right">{t('movements.qty')}</th>
                <th className="py-2.5 font-semibold text-right">{t('movements.rate')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {movements.length === 0 ? (
                <tr className="h-9">
                  <td colSpan={6} className="text-center text-slate-500">
                    {t('movements.empty')}
                  </td>
                </tr>
              ) : (
                movements.map(({ entry, line, key }) => (
                  <tr key={key} className="h-9 hover:bg-slate-50">
                    <td className="py-2 font-mono text-slate-600">{entry.postingDate}</td>
                    <td className="py-2 font-mono font-medium text-slate-900">{entry.voucherNo}</td>
                    <td className="py-2">
                      <span
                        className={`rounded px-2 py-0.5 text-[11px] font-medium ${ENTRY_TYPE_BADGE[entry.entryType]}`}
                      >
                        {t(`entryType.${entry.entryType}`)}
                      </span>
                    </td>
                    <td className="py-2 text-slate-700">
                      <span className="font-mono">{line.itemCode}</span> {line.itemName}
                    </td>
                    <td
                      className={`py-2 text-right font-mono font-bold ${
                        line.qty >= 0 ? 'text-emerald-600' : 'text-slate-700'
                      }`}
                    >
                      {line.qty >= 0 ? '+' : ''}
                      {formatQty(line.qty)}
                    </td>
                    <td className="py-2 text-right font-mono text-slate-600">
                      {line.rate === null ? t('movements.fifo') : formatMoney(line.rate)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {isModalOpen ? (
        <StockEntryModal
          companyId={companyId}
          items={items}
          leafWarehouses={leafWarehouses}
          defaultItemId={selectedItemId}
          onClose={() => setIsModalOpen(false)}
          onPosted={handlePosted}
        />
      ) : null}
    </div>
  )
}
