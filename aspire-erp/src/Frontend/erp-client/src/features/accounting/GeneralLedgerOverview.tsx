import { AlertTriangle, CheckCircle2, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTenantStore } from '../../store/useTenantStore'
import type { AccountTreeNode } from './types'
import { useAccountTree } from './useAccountTree'
import { useGeneralLedger } from './useGeneralLedger'

/** Same tolerance as `DoubleEntryImbalanceException` (|ΣD - ΣC| <= 0.0001). */
const BALANCE_TOLERANCE = 0.0001

/** USD display, matching `features/stock/format.ts` (dev seed currency). */
const formatCurrency = (value: number): string =>
  value.toLocaleString('en-US', { style: 'currency', currency: 'USD' })

/**
 * Delta display: keeps 2 decimals for normal amounts but allows up to 4, because a difference
 * just past the 0.0001 tolerance would otherwise render as a red badge reading "$0.00".
 */
const formatDelta = (value: number): string =>
  value.toLocaleString('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 4,
  })

/** Posting accounts only (`isGroup === false`), flattened and sorted by code for the dropdown. */
function collectPostingAccounts(nodes: AccountTreeNode[]): AccountTreeNode[] {
  return nodes
    .flatMap((node) => (node.isGroup ? collectPostingAccounts(node.children) : [node]))
    .sort((a, b) => a.code.localeCompare(b.code))
}

/** Active voucher drill-down: the id drives the `voucherId` query param, the no is display. */
interface VoucherDrillDown {
  voucherId: string
  voucherNo: string
}

/**
 * Immutable ledger audit viewer (Task 2.6).
 *
 * Reads the pinned `GET /api/v1/FinancialReports/general-ledger` contract: filters (date range,
 * account) and the voucher drill-down are combined client-side into query params (empty ones
 * skipped), so the server always answers with the rows AND the totals for that exact filter set.
 *
 * The footer totals are the server-side `totalDebit` / `totalCredit` over the FULL filtered set -
 * they are deliberately NOT recomputed from the visible items, because `take` (default 500) can
 * truncate the row list while the totals must still describe everything the filter matched.
 *
 * Rows arrive chronological (postingDate ASC, id ASC); clicking a row drills into that voucher
 * (originals + reversals) via the `voucherId` param, and the indicator strip clears it.
 */
export function GeneralLedgerOverview() {
  const companyId = useTenantStore((state) => state.companyId)
  const tenantId = useTenantStore((state) => state.tenantId)
  const { nodes, status: treeStatus } = useAccountTree(companyId)

  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [accountId, setAccountId] = useState('')
  const [drill, setDrill] = useState<VoucherDrillDown | null>(null)

  const accounts = useMemo(() => collectPostingAccounts(nodes), [nodes])

  const filters = useMemo(
    () => ({ companyId, accountId, voucherId: drill?.voucherId, from, to }),
    [companyId, accountId, drill, from, to],
  )
  const { report, status, error, reload } = useGeneralLedger(filters)

  const hasFilters = from !== '' || to !== '' || accountId !== '' || drill !== null
  const clearFilters = () => {
    setFrom('')
    setTo('')
    setAccountId('')
    setDrill(null)
  }
  const clearVoucher = () => setDrill(null)

  const drillDown = (entry: { voucherId: string; voucherNo: string }) =>
    setDrill({ voucherId: entry.voucherId, voucherNo: entry.voucherNo })

  const balanced = Math.abs(report.difference) <= BALANCE_TOLERANCE

  if (!companyId || !tenantId) {
    return (
      <p className="rounded-md border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        No tenant/company selected. Set <code>VITE_TENANT_ID</code> and{' '}
        <code>VITE_COMPANY_ID</code> in <code>.env.development</code>.
      </p>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-sky-50/50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-lg font-bold text-slate-900">General Ledger (`GLEntry`)</h2>
          <p className="mt-1 text-xs text-slate-600">
            Immutable, append-only double-entry financial ledger. Enforces invariant &Sigma;
            Debit = &Sigma; Credit.
          </p>
        </div>
        <p className="text-xs text-slate-500">Select a row to drill down into its voucher.</p>
      </div>

      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
        <label className="flex items-center gap-1.5 text-xs text-slate-500">
          From
          <input
            type="date"
            value={from}
            onChange={(event) => setFrom(event.target.value)}
            aria-label="From posting date"
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 focus:outline focus:outline-2 focus:outline-indigo-600"
          />
        </label>
        <label className="flex items-center gap-1.5 text-xs text-slate-500">
          To
          <input
            type="date"
            value={to}
            onChange={(event) => setTo(event.target.value)}
            aria-label="To posting date"
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 focus:outline focus:outline-2 focus:outline-indigo-600"
          />
        </label>
        <select
          value={accountId}
          onChange={(event) => setAccountId(event.target.value)}
          aria-label="Filter by account"
          className="h-8 max-w-64 min-w-0 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 focus:outline focus:outline-2 focus:outline-indigo-600"
        >
          <option value="">
            {treeStatus === 'loading'
              ? 'Loading accounts…'
              : treeStatus === 'error'
                ? 'All accounts (list unavailable)'
                : 'All accounts'}
          </option>
          {accounts.map((account) => (
            <option key={account.id} value={account.id}>
              {account.code} — {account.name}
            </option>
          ))}
        </select>
        {hasFilters ? (
          <button
            type="button"
            onClick={clearFilters}
            className="inline-flex h-8 items-center gap-1 rounded border border-slate-300 bg-white px-2 text-xs font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600"
          >
            <X className="size-3.5" aria-hidden="true" />
            Clear filters
          </button>
        ) : null}
      </div>

      {drill ? (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-800">
          <span>
            Showing voucher <span className="font-semibold">{drill.voucherNo}</span> — all lines,
            including reversals.
          </span>
          <button
            type="button"
            onClick={clearVoucher}
            className="inline-flex items-center gap-1 rounded border border-sky-300 bg-white px-2 py-1 font-medium text-sky-800 hover:bg-sky-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600"
          >
            <X className="size-3.5" aria-hidden="true" />
            Clear
          </button>
        </div>
      ) : null}

      {status === 'loading' ? (
        <p className="px-1 py-3 text-sm text-slate-500">Loading general ledger…</p>
      ) : status === 'error' ? (
        <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
          <p className="font-medium">
            Could not load the general ledger{error?.status ? ` (HTTP ${error.status})` : ''}.
          </p>
          {error?.code ? (
            <p className="mt-1">
              Error code:{' '}
              <code className="rounded bg-rose-100 px-1 py-0.5 font-mono text-xs">{error.code}</code>
            </p>
          ) : null}
          {error?.message ? <p className="mt-1">{error.message}</p> : null}
          <button
            type="button"
            onClick={() => {
              reload()
            }}
            className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
          >
            Retry
          </button>
        </div>
      ) : (
        <div className="rounded-lg border border-slate-200 bg-white shadow-xs">
          <div className="w-full overflow-x-auto">
            <table className="w-full min-w-[880px] text-left text-xs">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-3 py-2 font-semibold">Posting Date</th>
                  <th className="px-3 py-2 font-semibold">Account</th>
                  <th className="px-3 py-2 font-semibold">Voucher</th>
                  <th className="px-3 py-2 text-right font-semibold">Debit</th>
                  <th className="px-3 py-2 text-right font-semibold">Credit</th>
                  <th className="px-3 py-2 font-semibold">Remarks</th>
                </tr>
              </thead>
              <tbody>
                {report.items.length === 0 ? (
                  <tr className="h-9">
                    <td colSpan={6} className="px-4 text-center text-slate-500">
                      {drill ? (
                        `No entries found for voucher ${drill.voucherNo}.`
                      ) : hasFilters ? (
                        <span>
                          No entries match the current filters.{' '}
                          <button
                            type="button"
                            onClick={clearFilters}
                            className="font-medium text-indigo-600 underline-offset-2 hover:underline"
                          >
                            Clear filters
                          </button>
                        </span>
                      ) : (
                        'No ledger entries yet for this company.'
                      )}
                    </td>
                  </tr>
                ) : (
                  report.items.map((entry) => (
                    <tr
                      key={entry.id}
                      onClick={() => drillDown(entry)}
                      className="h-9 cursor-pointer border-b border-slate-100 last:border-b-0 hover:bg-slate-50"
                    >
                      <td className="px-3 font-mono text-slate-600">{entry.postingDate}</td>
                      <td className="px-3 text-slate-900" title={`${entry.accountCode} — ${entry.accountName}`}>
                        <span className="font-mono text-slate-600">{entry.accountCode}</span>
                        <span
                          className="ml-2 inline-block max-w-[200px] truncate align-bottom"
                          title={entry.accountName}
                        >
                          {entry.accountName}
                        </span>
                      </td>
                      <td className="px-3">
                        <span className="mr-1.5 inline-flex items-center rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-slate-600">
                          {entry.voucherType}
                        </span>
                        <button
                          type="button"
                          onClick={(event) => {
                            event.stopPropagation()
                            drillDown(entry)
                          }}
                          className="font-mono text-sky-600 underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600"
                        >
                          {entry.voucherNo}
                        </button>
                        {entry.isCancelled ? (
                          <span className="ml-1.5 inline-flex items-center rounded bg-rose-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-rose-700">
                            Reversal
                          </span>
                        ) : null}
                      </td>
                      <td
                        className="px-3 text-right font-mono text-slate-900"
                        title={`${entry.accountCurrency} ${formatCurrency(entry.debit)}`}
                      >
                        {formatCurrency(entry.debit)}
                      </td>
                      <td
                        className="px-3 text-right font-mono text-slate-900"
                        title={`${entry.accountCurrency} ${formatCurrency(entry.credit)}`}
                      >
                        {formatCurrency(entry.credit)}
                      </td>
                      <td
                        className="max-w-[240px] truncate px-3 text-slate-600"
                        title={entry.remarks ?? undefined}
                      >
                        {entry.remarks ?? '—'}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          {report.items.length > 0 ? (
            <div className="flex flex-wrap items-center justify-between gap-2 border-t-2 border-slate-300 px-3 py-2">
              <span
                className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${
                  balanced ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
                }`}
              >
                {balanced ? (
                  <>
                    <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                    Balanced
                  </>
                ) : (
                  <>
                    <AlertTriangle className="h-4 w-4" aria-hidden="true" />
                    Out of balance by {formatDelta(report.difference)}
                  </>
                )}
              </span>
              <div className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-600">
                <span>
                  Total debits:{' '}
                  <strong className="font-mono text-slate-900">
                    {formatCurrency(report.totalDebit)}
                  </strong>
                </span>
                <span>
                  Total credits:{' '}
                  <strong className="font-mono text-slate-900">
                    {formatCurrency(report.totalCredit)}
                  </strong>
                </span>
              </div>
            </div>
          ) : null}
        </div>
      )}
    </div>
  )
}
