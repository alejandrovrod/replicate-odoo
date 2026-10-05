import { AlertTriangle, CheckCircle2, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useTenantStore } from '../../store/useTenantStore'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { formatDelta, formatMoney } from '../../lib/format'
import type { AccountTreeNode } from './types'
import { useAccountTree } from './useAccountTree'
import { useGeneralLedger } from './useGeneralLedger'

/** Same tolerance as `DoubleEntryImbalanceException` (|ΣD - ΣC| <= 0.0001). */
const BALANCE_TOLERANCE = 0.0001

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
  const { t, i18n } = useTranslation('accounting')
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
        {t('missingTenant')}
      </p>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-sky-50/50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-lg font-bold text-slate-900">{t('ledger.title')}</h2>
          <p className="mt-1 text-xs text-slate-600">{t('ledger.subtitle')}</p>
        </div>
        <p className="text-xs text-slate-500">{t('ledger.drillHint')}</p>
      </div>

      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
        <label className="flex items-center gap-1.5 text-xs text-slate-500">
          {t('ledger.from')}
          <input
            type="date"
            value={from}
            onChange={(event) => setFrom(event.target.value)}
            aria-label={t('ledger.fromAria')}
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 focus:outline focus:outline-2 focus:outline-indigo-600"
          />
        </label>
        <label className="flex items-center gap-1.5 text-xs text-slate-500">
          {t('ledger.to')}
          <input
            type="date"
            value={to}
            onChange={(event) => setTo(event.target.value)}
            aria-label={t('ledger.toAria')}
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 focus:outline focus:outline-2 focus:outline-indigo-600"
          />
        </label>
        <select
          value={accountId}
          onChange={(event) => setAccountId(event.target.value)}
          aria-label={t('ledger.filterByAccount')}
          className="h-8 max-w-64 min-w-0 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 focus:outline focus:outline-2 focus:outline-indigo-600"
        >
          <option value="">
            {treeStatus === 'loading'
              ? t('ledger.loadingAccounts')
              : treeStatus === 'error'
                ? t('ledger.listUnavailable')
                : t('ledger.allAccounts')}
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
            {t('ledger.clearFilters')}
          </button>
        ) : null}
      </div>

      {drill ? (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-800">
          <span>{t('ledger.drillVoucher', { no: drill.voucherNo })}</span>
          <button
            type="button"
            onClick={clearVoucher}
            className="inline-flex items-center gap-1 rounded border border-sky-300 bg-white px-2 py-1 font-medium text-sky-800 hover:bg-sky-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600"
          >
            <X className="size-3.5" aria-hidden="true" />
            {t('ledger.clear')}
          </button>
        </div>
      ) : null}

      {status === 'loading' ? (
        <p className="px-1 py-3 text-sm text-slate-500">{t('ledger.loading')}</p>
      ) : status === 'error' ? (
        <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
          <p className="font-medium">
            {t('ledger.loadFailed')}
            {error?.status ? ` (HTTP ${error.status})` : ''}.
          </p>
          {error?.code ? (
            <p className="mt-1">
              {t('ledger.errorCode')}{' '}
              <code className="rounded bg-rose-100 px-1 py-0.5 font-mono text-xs">{error.code}</code>
            </p>
          ) : null}
          {error?.message ? <p className="mt-1">{translateErrorCode(i18n, error.code, error.message)}</p> : null}
          <button
            type="button"
            onClick={() => {
              reload()
            }}
            className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
          >
            {t('ledger.retry')}
          </button>
        </div>
      ) : (
        <div className="rounded-lg border border-slate-200 bg-white shadow-xs">
          <div className="w-full overflow-x-auto">
            <table className="w-full min-w-[880px] text-left text-xs">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-3 py-2 font-semibold">{t('ledger.colDate')}</th>
                  <th className="px-3 py-2 font-semibold">{t('ledger.colAccount')}</th>
                  <th className="px-3 py-2 font-semibold">{t('ledger.colVoucher')}</th>
                  <th className="px-3 py-2 text-right font-semibold">{t('ledger.colDebit')}</th>
                  <th className="px-3 py-2 text-right font-semibold">{t('ledger.colCredit')}</th>
                  <th className="px-3 py-2 font-semibold">{t('ledger.colRemarks')}</th>
                </tr>
              </thead>
              <tbody>
                {report.items.length === 0 ? (
                  <tr className="h-9">
                    <td colSpan={6} className="px-4 text-center text-slate-500">
                      {drill ? (
                        t('ledger.noVoucherEntries', { no: drill.voucherNo })
                      ) : hasFilters ? (
                        <span>
                          {t('ledger.noFiltered')}{' '}
                          <button
                            type="button"
                            onClick={clearFilters}
                            className="font-medium text-indigo-600 underline-offset-2 hover:underline"
                          >
                            {t('ledger.clearFilters')}
                          </button>
                        </span>
                      ) : (
                        t('ledger.noEntries')
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
                            {t('ledger.reversal')}
                          </span>
                        ) : null}
                      </td>
                      <td
                        className="px-3 text-right font-mono text-slate-900"
                        title={`${entry.accountCurrency} ${formatMoney(entry.debit)}`}
                      >
                        {formatMoney(entry.debit)}
                      </td>
                      <td
                        className="px-3 text-right font-mono text-slate-900"
                        title={`${entry.accountCurrency} ${formatMoney(entry.credit)}`}
                      >
                        {formatMoney(entry.credit)}
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
                    {t('ledger.balanced')}
                  </>
                ) : (
                  <>
                    <AlertTriangle className="h-4 w-4" aria-hidden="true" />
                    {t('ledger.outOfBalance', { amount: formatDelta(report.difference) })}
                  </>
                )}
              </span>
              <div className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-600">
                <span>
                  {t('ledger.totalDebits')}{' '}
                  <strong className="font-mono text-slate-900">
                    {formatMoney(report.totalDebit)}
                  </strong>
                </span>
                <span>
                  {t('ledger.totalCredits')}{' '}
                  <strong className="font-mono text-slate-900">
                    {formatMoney(report.totalCredit)}
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
