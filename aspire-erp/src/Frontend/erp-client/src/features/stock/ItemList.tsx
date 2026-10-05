import { Plus } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { ApiError } from '../../api/client'
import { formatMoney, formatQty } from './format'
import { flattenWarehouses, type Item, type WarehouseNode } from './types'
import type { QueryStatus } from './useStockData'

interface ItemListProps {
  items: Item[]
  warehouses: WarehouseNode[]
  status: QueryStatus
  error: ApiError | null
  onReload: () => void
  /** Opens the StockEntryModal; `itemId` preselects a line when the row button is used. */
  onNewEntry: (itemId: string | null) => void
}

/**
 * Item list with live stock levels (Task 3.4). Each leaf warehouse gets its own column so the
 * per-location balance is readable at a glance, plus the row totals the valuation is checked
 * against. `status`/`error`/`onReload` mirror the `AccountTreeTable` loading contract, and a
 * posted voucher makes the container reload this table - that is the "updated stock levels in
 * real time" acceptance case.
 */
export function ItemList({ items, warehouses, status, error, onReload, onNewEntry }: ItemListProps) {
  const { t } = useTranslation('stock')
  const leafWarehouses = flattenWarehouses(warehouses, true)

  if (status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          {t('items.loadFailed')}
          {error?.status ? ` (HTTP ${error.status})` : ''}.
        </p>
        {error?.message ? <p className="mt-1">{error.message}</p> : null}
        <button
          type="button"
          onClick={() => {
            onReload()
          }}
          className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
        >
          {t('items.retry')}
        </button>
      </div>
    )
  }

  if (status === 'loading') {
    return <p className="px-1 py-3 text-sm text-slate-500">{t('items.loading')}</p>
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200">
      <table className="w-full min-w-[720px] text-left text-xs">
        <thead>
          <tr className="border-b border-slate-200 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
            <th className="px-3 py-2 font-semibold">{t('items.status')}</th>
            <th className="px-3 py-2 font-semibold">{t('items.code')}</th>
            <th className="px-3 py-2 font-semibold">{t('items.item')}</th>
            <th className="px-3 py-2 font-semibold">{t('items.valuation')}</th>
            {leafWarehouses.map((warehouse) => (
              <th
                key={warehouse.id}
                className="px-3 py-2 text-right font-semibold"
                title={warehouse.name}
              >
                {warehouse.code}
              </th>
            ))}
            <th className="px-3 py-2 text-right font-semibold">{t('items.onHand')}</th>
            <th className="px-3 py-2 text-right font-semibold">{t('items.value')}</th>
            <th className="px-3 py-2" />
          </tr>
        </thead>
        <tbody>
          {items.length === 0 ? (
            <tr className="h-9">
              <td
                colSpan={7 + leafWarehouses.length}
                className="px-4 text-center text-slate-500"
              >
                {t('items.empty')}
              </td>
            </tr>
          ) : (
            items.map((item) => {
              const byWarehouse = new Map(item.stock.map((row) => [row.warehouseId, row]))
              const totalQty = item.stock.reduce((sum, row) => sum + row.qty, 0)
              const totalValue = item.stock.reduce((sum, row) => sum + row.value, 0)

              return (
                <tr
                  key={item.id}
                  className="h-9 border-b border-slate-100 last:border-b-0 hover:bg-slate-50"
                >
                  <td className="px-3">
                    <span
                      className={`inline-flex items-center gap-1.5 text-xs ${
                        item.isActive ? 'text-emerald-700' : 'text-slate-400'
                      }`}
                    >
                      <span
                        className={`size-2 rounded-full ${
                          item.isActive ? 'bg-emerald-500' : 'bg-slate-300'
                        }`}
                        aria-hidden="true"
                      />
                      {item.isActive ? t('items.active') : t('items.inactive')}
                    </span>
                  </td>
                  <td className="px-3 font-mono text-slate-700">{item.code}</td>
                  <td className="max-w-[220px] truncate px-3 text-slate-900" title={item.name}>
                    {item.name}
                  </td>
                  <td className="px-3">
                      <span className="inline-flex items-center rounded bg-sky-100 px-2 py-0.5 text-xs font-medium text-sky-700">
                        {item.valuationMethod === 'Fifo' ? t('items.fifo') : item.valuationMethod}
                      </span>
                  </td>
                  {leafWarehouses.map((warehouse) => {
                    const row = byWarehouse.get(warehouse.id)
                    if (!row) {
                      return (
                        <td
                          key={warehouse.id}
                          className="px-3 text-right font-mono text-slate-300"
                          title={t('items.noMovement')}
                        >
                          —
                        </td>
                      )
                    }
                    return (
                      <td
                        key={warehouse.id}
                        className={`px-3 text-right font-mono ${
                          row.qty > 0 ? 'font-medium text-emerald-700' : 'text-slate-400'
                        }`}
                        title={`${warehouse.name}: ${formatMoney(row.value)}`}
                      >
                        {formatQty(row.qty)}
                      </td>
                    )
                  })}
                  <td className="px-3 text-right font-mono font-semibold text-slate-900">
                    {formatQty(totalQty)}
                  </td>
                  <td className="px-3 text-right font-mono font-semibold text-slate-900">
                    {formatMoney(totalValue)}
                  </td>
                  <td className="px-3 text-right">
                    <button
                      type="button"
                      onClick={() => onNewEntry(item.id)}
                      className="inline-flex items-center gap-1 rounded border border-slate-200 bg-white px-2 py-1 text-xs font-medium text-slate-700 hover:border-indigo-300 hover:bg-indigo-50 hover:text-indigo-700"
                    >
                      <Plus className="size-3" aria-hidden="true" />
                      {t('items.newEntry')}
                    </button>
                  </td>
                </tr>
              )
            })
          )}
        </tbody>
      </table>
    </div>
  )
}
