import { Boxes, CheckCircle2, Layers, Plus, Warehouse, X } from 'lucide-react'
import { useMemo, useState } from 'react'
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
        No tenant/company selected. Set <code>VITE_TENANT_ID</code> and{' '}
        <code>VITE_COMPANY_ID</code> in <code>.env.development</code>.
      </p>
    )
  }

  if (itemsQuery.status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          Could not load the inventory{itemsQuery.error?.status ? ` (HTTP ${itemsQuery.error.status})` : ''}.
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
          Retry
        </button>
      </div>
    )
  }

  if (itemsQuery.status !== 'success') {
    return <p className="px-1 py-3 text-sm text-slate-500">Loading inventory…</p>
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-slate-900">Stock & Inventory</h2>
          <p className="text-xs text-slate-500">
            Perpetual inventory: posting a voucher writes its Kardex rows and balanced General
            Ledger lines in the same transaction.
          </p>
        </div>
        <button
          type="button"
          onClick={() => openModal(null)}
          className="inline-flex items-center gap-1.5 rounded bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          <Plus className="size-4" aria-hidden="true" />
          New stock entry
        </button>
      </div>

      {/* Post confirmation: proves the double-entry balance and that the reload happened */}
      {lastPosted ? (
        <div className="flex items-start justify-between gap-3 rounded-md border border-emerald-300 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
          <p className="flex items-start gap-2">
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>
              <span className="font-semibold">Posted {lastPosted.entry.voucherNo}</span> — Dr{' '}
              {formatMoney(lastPosted.totalDebit)} = Cr {formatMoney(lastPosted.totalCredit)} (
              {lastPosted.glEntries.length} GL lines, {lastPosted.ledgerEntries.length} Kardex
              rows). Stock levels below were refreshed.
            </span>
          </p>
          <button
            type="button"
            onClick={() => setLastPosted(null)}
            aria-label="Dismiss"
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
            <span className="text-xs font-medium">Warehouses</span>
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900">
            {leafWarehouses.length} Location{leafWarehouses.length === 1 ? '' : 's'}
          </p>
          <span className="text-xs text-slate-400">Leaf nodes of the warehouse tree</span>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
          <div className="flex items-center gap-2 text-slate-500">
            <Boxes className="h-4 w-4 text-sky-600" />
            <span className="text-xs font-medium">Active SKUs</span>
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900">{activeSkus} Active Items</p>
          <span className="text-xs text-slate-400">Unique SKU per tenant (Task 3.1)</span>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
          <div className="flex items-center gap-2 text-slate-500">
            <Layers className="h-4 w-4 text-emerald-600" />
            <span className="text-xs font-medium">Stock Valuation</span>
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900">{formatMoney(totalValue)}</p>
          <span className="text-xs text-slate-400">FIFO layers in Stock In Hand (1310)</span>
        </div>
      </div>

      {/* Warehouse grid */}
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-semibold text-slate-900">Warehouses</h3>
        <p className="mb-4 text-xs text-slate-500">
          Stock balance and perpetual valuation per leaf location.
        </p>
        {leafWarehouses.length === 0 ? (
          <p className="text-sm text-slate-500">No warehouses yet for this company.</p>
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
                  {warehouse.isActive ? 'Active' : 'Inactive'} · stock account{' '}
                  {warehouse.stockAccountId.slice(-4)}
                </p>
                <div className="mt-4 flex items-baseline justify-between border-t border-slate-200 pt-3 text-xs">
                  <span className="text-slate-500">{stockedSkus(warehouse.id)} SKUs on hand</span>
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
            <h3 className="text-base font-semibold text-slate-900">Items & Stock Levels</h3>
            <p className="text-xs text-slate-500">
              Live SUM of the Kardex (<code>StockLedgerEntry</code>) per warehouse. Posting an
              entry reloads this table.
            </p>
          </div>
          <button
            type="button"
            onClick={() => openModal(null)}
            className="inline-flex items-center gap-1 rounded border border-slate-300 px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            <Plus className="size-3" aria-hidden="true" />
            New stock entry
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
        <h3 className="text-base font-semibold text-slate-900">Recent Movements</h3>
        <p className="text-xs text-slate-500">Posted vouchers, newest first.</p>

        <div className="mt-4 overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">Date</th>
                <th className="py-2.5 font-semibold">Voucher</th>
                <th className="py-2.5 font-semibold">Type</th>
                <th className="py-2.5 font-semibold">Item</th>
                <th className="py-2.5 font-semibold text-right">Movement Qty</th>
                <th className="py-2.5 font-semibold text-right">Unit Rate</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {movements.length === 0 ? (
                <tr className="h-9">
                  <td colSpan={6} className="text-center text-slate-500">
                    No stock movements yet — post a Material Receipt to open the ledger.
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
                        {entry.entryType}
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
                      {line.rate === null ? 'FIFO' : formatMoney(line.rate)}
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
