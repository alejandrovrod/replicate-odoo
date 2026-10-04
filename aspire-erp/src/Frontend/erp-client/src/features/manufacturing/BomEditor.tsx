import { Boxes, Cog, GitBranch } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { Bom } from './types'
import { useBoms } from './useManufacturingData'

const money = (value: number): string =>
  value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 4 })

/**
 * BOM Studio tree (Task 9.5): a read-only recipe viewer over GET /api/v1/boms - the
 * selector picks a recipe, the tree shows its component lines with the persisted cost
 * roll-up and its workstation operations with the engineered cost per step.
 *
 * Read-only BY DESIGN: no BOM-CRUD command or endpoint exists (BOM rows are engineering
 * masters seeded per test), so there is no create/update UI to build. Adding a write
 * path is out of scope.
 */
export function BomEditor({ companyId }: { companyId: string }) {
  const bomsQuery = useBoms(companyId)
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const selected: Bom | null = useMemo(() => {
    if (bomsQuery.data.length === 0) return null
    return bomsQuery.data.find((b) => b.id === selectedId) ?? bomsQuery.data[0]
  }, [bomsQuery.data, selectedId])

  if (bomsQuery.status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          Could not load recipes{bomsQuery.error?.status ? ` (HTTP ${bomsQuery.error.status})` : ''}.
        </p>
        {bomsQuery.error?.message ? <p className="mt-1">{bomsQuery.error.message}</p> : null}
        <button
          type="button"
          onClick={() => bomsQuery.reload()}
          className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
        >
          Retry
        </button>
      </div>
    )
  }

  if (bomsQuery.status !== 'success') {
    return <p className="px-1 py-3 text-sm text-slate-500">Loading recipes…</p>
  }

  if (bomsQuery.data.length === 0) {
    return (
      <p className="rounded-md border border-slate-200 bg-white px-4 py-6 text-center text-sm text-slate-500">
        No bills of materials yet for this company. Recipes are engineering masters (no
        create endpoint exists) — seed one per the manufacturing seed script.
      </p>
    )
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h3 className="flex items-center gap-2 text-base font-semibold text-slate-900">
            <GitBranch className="h-4 w-4 text-indigo-600" />
            BOM Studio
          </h3>
          <p className="text-xs text-slate-500">
            Recipe tree: components, operations and the persisted cost roll-up (read-only).
          </p>
        </div>
        <label className="flex items-center gap-2 text-xs font-medium text-slate-600">
          Recipe
          <select
            value={selected?.id ?? ''}
            onChange={(e) => setSelectedId(e.target.value)}
            className="rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 font-mono text-xs text-slate-800"
          >
            {bomsQuery.data.map((bom) => (
              <option key={bom.id} value={bom.id}>
                {bom.bomNumber} · {bom.itemCode}
              </option>
            ))}
          </select>
        </label>
      </div>

      {selected ? (
        <div className="space-y-5">
          {/* Header + roll-up */}
          <div className="flex flex-wrap items-center gap-2 rounded-lg bg-slate-50 p-4">
            <span className="font-mono text-sm font-bold text-slate-900">{selected.bomNumber}</span>
            <span className="text-xs text-slate-500">
              yields {selected.quantity} × {selected.itemCode} {selected.itemName}
            </span>
            <span
              className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                selected.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-600'
              }`}
            >
              {selected.isActive ? 'Active' : 'Inactive'}
            </span>
            {selected.isDefault ? (
              <span className="rounded-full bg-indigo-100 px-2 py-0.5 text-[11px] font-semibold text-indigo-800">
                Default
              </span>
            ) : null}
            <span className="ml-auto font-mono text-sm font-bold text-slate-900">
              ${money(selected.totalCost)}
            </span>
          </div>

          <div className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-4">
            {[
              ['Materials', selected.rawMaterialCost],
              ['Operations', selected.operatingCost],
              ['Scrap', selected.scrapCost],
              ['Total', selected.totalCost],
            ].map(([label, value]) => (
              <div key={label as string} className="rounded-lg border border-slate-100 bg-white p-3">
                <p className="text-slate-400">{label}</p>
                <p className="mt-1 font-mono text-sm font-bold text-slate-900">${money(value as number)}</p>
              </div>
            ))}
          </div>

          {/* Component lines */}
          <div>
            <h4 className="mb-2 flex items-center gap-1.5 text-xs font-semibold text-slate-700 uppercase tracking-wide">
              <Boxes className="h-3.5 w-3.5 text-slate-400" /> Components
            </h4>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-200 text-slate-400">
                    <th className="py-2 font-semibold">Item</th>
                    <th className="py-2 text-right font-semibold">Qty</th>
                    <th className="py-2 text-right font-semibold">Rate</th>
                    <th className="py-2 text-right font-semibold">Amount</th>
                    <th className="py-2 text-right font-semibold">Scrap %</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {selected.items.map((line) => (
                    <tr key={line.id} className="h-9 hover:bg-slate-50">
                      <td className="py-2 text-slate-700">
                        <span className="font-mono font-medium text-slate-900">{line.itemCode}</span>{' '}
                        {line.itemName}
                      </td>
                      <td className="py-2 text-right font-mono">{line.quantity}</td>
                      <td className="py-2 text-right font-mono">${money(line.valuationRate)}</td>
                      <td className="py-2 text-right font-mono font-bold">${money(line.amount)}</td>
                      <td className="py-2 text-right font-mono text-slate-500">{line.scrapPercentage}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Operations */}
          <div>
            <h4 className="mb-2 flex items-center gap-1.5 text-xs font-semibold text-slate-700 uppercase tracking-wide">
              <Cog className="h-3.5 w-3.5 text-slate-400" /> Operations
            </h4>
            {selected.operations.length === 0 ? (
              <p className="text-xs text-slate-500">No workstation steps on this recipe.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead>
                    <tr className="border-b border-slate-200 text-slate-400">
                      <th className="py-2 font-semibold">Step</th>
                      <th className="py-2 font-semibold">Workstation</th>
                      <th className="py-2 text-right font-semibold">Minutes</th>
                      <th className="py-2 text-right font-semibold">Rate/h</th>
                      <th className="py-2 text-right font-semibold">Cost</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {selected.operations.map((op) => (
                      <tr key={op.id} className="h-9 hover:bg-slate-50">
                        <td className="py-2 text-slate-700">{op.description ?? '—'}</td>
                        <td className="py-2 font-medium text-slate-900">{op.workstationName}</td>
                        <td className="py-2 text-right font-mono">{op.durationMinutes}</td>
                        <td className="py-2 text-right font-mono">${money(op.hourRateTotal)}</td>
                        <td className="py-2 text-right font-mono font-bold">${money(op.operationCost)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      ) : null}
    </div>
  )
}
