import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useTenantStore } from '../../../store/useTenantStore'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { Input } from '../../../components/ui/Input'
import {
  todayIso,
  useAgingReport,
  useBalanceSheet,
  useProfitAndLoss,
  useStockLedgerReport,
  useTrialBalance,
  type FinancialSection,
} from '../api/useReports'

type ReportTab = 'balance' | 'pnl' | 'trial' | 'aging' | 'kardex'

const money = (v: number): string =>
  v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

function SectionTable({ title, total, section }: { title: string; total: number; section: FinancialSection }) {
  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xs">
      <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
        <h4 className="text-sm font-semibold text-slate-900">{title}</h4>
        <span className="font-mono text-sm font-bold text-slate-900">{money(total)}</span>
      </div>
      <Table>
        <TableBody>
          {section.rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={3} className="h-16 text-center text-xs text-slate-400">
                —
              </TableCell>
            </TableRow>
          ) : (
            section.rows.map((r) => (
              <TableRow key={r.accountId} className="hover:bg-slate-50">
                <TableCell className="font-mono text-xs text-slate-500">{r.accountCode}</TableCell>
                <TableCell className="text-xs text-slate-800">{r.accountName}</TableCell>
                <TableCell className="text-right font-mono text-xs font-semibold text-slate-900">
                  {money(r.balance)}
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  )
}

export function ReportsView() {
  const { t } = useTranslation('dashboard')
  const companyId = useTenantStore((state) => state.companyId)
  const [tab, setTab] = useState<ReportTab>('balance')
  const [asOf, setAsOf] = useState(todayIso())
  const [from, setFrom] = useState(() => {
    const d = new Date()
    return `${d.getFullYear()}-01-01`
  })
  const [to, setTo] = useState(todayIso())

  const trial = useTrialBalance(companyId, tab === 'trial' || tab === 'balance' ? asOf : '')
  const balance = useBalanceSheet(companyId, tab === 'balance' ? asOf : '')
  const pnl = useProfitAndLoss(companyId, tab === 'pnl' ? from : '', tab === 'pnl' ? to : '')
  const aging = useAgingReport(companyId, tab === 'aging' ? asOf : '')
  const kardex = useStockLedgerReport(companyId, tab === 'kardex' ? from : '', tab === 'kardex' ? to : '')

  const tabs: { id: ReportTab; label: string }[] = [
    { id: 'balance', label: t('reports.balance', 'Balance Sheet') },
    { id: 'pnl', label: t('reports.pnl', 'Profit & Loss') },
    { id: 'trial', label: t('reports.trial', 'Trial Balance') },
    { id: 'aging', label: t('reports.aging', 'Aging AR/AP') },
    { id: 'kardex', label: t('reports.kardex', 'Stock Ledger') },
  ]

  return (
    <div className="space-y-4 p-4">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">{t('reports.title', 'Financial Reports')}</h2>
        <p className="text-xs text-slate-500">{t('reports.subtitle', 'Read-only statements aggregated from the ledger')}</p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {tabs.map((tb) => (
          <button
            key={tb.id}
            type="button"
            onClick={() => setTab(tb.id)}
            className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors ${
              tab === tb.id ? 'bg-sky-600 text-white shadow-xs' : 'bg-white text-slate-600 border border-slate-200 hover:bg-slate-50'
            }`}
          >
            {tb.label}
          </button>
        ))}
      </div>

      {(tab === 'balance' || tab === 'trial' || tab === 'aging') && (
        <div className="flex items-center gap-2">
          <label className="text-xs font-medium text-slate-600">
            {tab === 'aging' ? t('reports.reportDate', 'Report date') : t('reports.asOf', 'As of')}
          </label>
          <Input type="date" value={asOf} onChange={(e) => setAsOf(e.target.value)} className="max-w-45" />
        </div>
      )}
      {(tab === 'pnl' || tab === 'kardex') && (
        <div className="flex flex-wrap items-center gap-2">
          <label className="text-xs font-medium text-slate-600">{t('reports.from', 'From')}</label>
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="max-w-45" />
          <label className="text-xs font-medium text-slate-600">{t('reports.to', 'To')}</label>
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="max-w-45" />
        </div>
      )}

      {tab === 'balance' && (
        <div>
          {balance.status === 'loading' && <p className="text-xs text-slate-500">{t('reports.loading', 'Loading…')}</p>}
          {balance.status === 'error' && <p className="text-xs text-red-600">{balance.error?.message}</p>}
          {balance.data && (
            <div className="space-y-4">
              <div className={`rounded-xl border px-4 py-3 text-xs font-semibold ${balance.data.balanced ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-amber-200 bg-amber-50 text-amber-800'}`}>
                {balance.data.balanced
                  ? t('reports.balanced', 'Balanced: Assets = Liabilities + Equity')
                  : t('reports.unbalanced', 'Out of balance by the unclosed period profit')}
              </div>
              <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
                <SectionTable title={t('reports.assets', 'Assets')} total={balance.data.assets.total} section={balance.data.assets} />
                <SectionTable title={t('reports.liabilities', 'Liabilities')} total={balance.data.liabilities.total} section={balance.data.liabilities} />
                <SectionTable title={t('reports.equity', 'Equity')} total={balance.data.equity.total} section={balance.data.equity} />
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'pnl' && (
        <div>
          {pnl.status === 'loading' && <p className="text-xs text-slate-500">{t('reports.loading', 'Loading…')}</p>}
          {pnl.status === 'error' && <p className="text-xs text-red-600">{pnl.error?.message}</p>}
          {pnl.data && (
            <div className="space-y-4">
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 flex items-center justify-between">
                <span className="text-sm font-semibold text-slate-900">{t('reports.netProfit', 'Net Profit')}</span>
                <span className={`font-mono text-lg font-bold ${pnl.data.netProfit >= 0 ? 'text-emerald-700' : 'text-rose-700'}`}>
                  {money(pnl.data.netProfit)}
                </span>
              </div>
              <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
                <SectionTable title={t('reports.revenue', 'Revenue')} total={pnl.data.revenue.total} section={pnl.data.revenue} />
                <SectionTable title={t('reports.cogs', 'COGS')} total={pnl.data.cogs.total} section={pnl.data.cogs} />
                <SectionTable title={t('reports.expenses', 'Expenses')} total={pnl.data.expenses.total} section={pnl.data.expenses} />
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'trial' && (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xs">
          {trial.status === 'loading' && <p className="p-4 text-xs text-slate-500">{t('reports.loading', 'Loading…')}</p>}
          {trial.status === 'error' && <p className="p-4 text-xs text-red-600">{trial.error?.message}</p>}
          {trial.data && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('reports.colAccount', 'Account')}</TableHead>
                  <TableHead>{t('reports.colName', 'Name')}</TableHead>
                  <TableHead className="text-right">{t('reports.colDebit', 'Debit')}</TableHead>
                  <TableHead className="text-right">{t('reports.colCredit', 'Credit')}</TableHead>
                  <TableHead className="text-right">{t('reports.colBalance', 'Balance')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {trial.data.rows.map((r) => (
                  <TableRow key={r.accountId} className="hover:bg-slate-50">
                    <TableCell className="font-mono text-xs">{r.accountCode}</TableCell>
                    <TableCell className="text-xs">{r.accountName}</TableCell>
                    <TableCell className="text-right font-mono text-xs">{money(r.totalDebit)}</TableCell>
                    <TableCell className="text-right font-mono text-xs">{money(r.totalCredit)}</TableCell>
                    <TableCell className="text-right font-mono text-xs font-semibold">{money(r.netBalance)}</TableCell>
                  </TableRow>
                ))}
                <TableRow className="bg-slate-50 font-semibold">
                  <TableCell colSpan={2} className="text-xs">{t('reports.total', 'Total')}</TableCell>
                  <TableCell className="text-right font-mono text-xs">{money(trial.data.totalDebit)}</TableCell>
                  <TableCell className="text-right font-mono text-xs">{money(trial.data.totalCredit)}</TableCell>
                  <TableCell className={`text-right font-mono text-xs ${trial.data.difference === 0 ? 'text-emerald-700' : 'text-rose-700'}`}>
                    {money(trial.data.difference)}
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          )}
        </div>
      )}

      {tab === 'aging' && (
        <div className="space-y-4">
          {aging.status === 'loading' && <p className="text-xs text-slate-500">{t('reports.loading', 'Loading…')}</p>}
          {aging.status === 'error' && <p className="text-xs text-red-600">{aging.error?.message}</p>}
          {aging.data && (
            <>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                {(
                  [
                    { leg: t('reports.receivable', 'Receivable'), totals: aging.data.totals.receivable },
                    { leg: t('reports.payable', 'Payable'), totals: aging.data.totals.payable },
                  ] as const
                ).map(({ leg, totals }) => (
                  <div key={leg} className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
                    <h4 className="text-sm font-semibold text-slate-900">{leg} · {money(totals.outstanding)}</h4>
                    <div className="mt-2 grid grid-cols-3 gap-2 text-center">
                      {(
                        [
                          [t('reports.notDue', 'Not due'), totals.notDue],
                          ['0-30', totals.range030],
                          ['31-60', totals.range3160],
                          ['61-90', totals.range6190],
                          ['90+', totals.range90Plus],
                        ] as const
                      ).map(([label, value]) => (
                        <div key={label} className="rounded-lg bg-slate-50 px-2 py-2">
                          <div className="text-[10px] font-semibold uppercase text-slate-400">{label}</div>
                          <div className="font-mono text-xs font-bold text-slate-800">{money(value)}</div>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
              <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xs">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('reports.colParty', 'Party')}</TableHead>
                      <TableHead>{t('reports.colVoucher', 'Voucher')}</TableHead>
                      <TableHead>{t('reports.colDue', 'Due')}</TableHead>
                      <TableHead className="text-right">{t('reports.colInvoiced', 'Invoiced')}</TableHead>
                      <TableHead className="text-right">{t('reports.colOutstanding', 'Outstanding')}</TableHead>
                      <TableHead className="text-right">{t('reports.colAge', 'Age')}</TableHead>
                      <TableHead className="text-right">{t('reports.colBucket', 'Bucket')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {aging.data.rows.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={7} className="h-16 text-center text-xs text-slate-400">
                          {t('reports.empty', 'Nothing open.')}
                        </TableCell>
                      </TableRow>
                    ) : (
                      aging.data.rows.map((r) => (
                        <TableRow key={`${r.voucherType}-${r.voucherNo}`} className="hover:bg-slate-50">
                          <TableCell className="text-xs">
                            <span className="mr-1 rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold text-slate-600">
                              {r.partyType}
                            </span>
                            {r.partyName}
                          </TableCell>
                          <TableCell className="font-mono text-xs">{r.voucherNo}</TableCell>
                          <TableCell className="font-mono text-xs text-slate-500">{r.dueDate}</TableCell>
                          <TableCell className="text-right font-mono text-xs">{money(r.invoicedAmount)}</TableCell>
                          <TableCell className="text-right font-mono text-xs font-bold">{money(r.outstandingAmount)}</TableCell>
                          <TableCell className="text-right font-mono text-xs">{r.ageDays}</TableCell>
                          <TableCell className="text-right font-mono text-[11px] text-slate-600">{r.bucket}</TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
            </>
          )}
        </div>
      )}

      {tab === 'kardex' && (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xs">
          {kardex.status === 'loading' && <p className="p-4 text-xs text-slate-500">{t('reports.loading', 'Loading…')}</p>}
          {kardex.status === 'error' && <p className="p-4 text-xs text-red-600">{kardex.error?.message}</p>}
          {kardex.data && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('reports.colDate', 'Date')}</TableHead>
                  <TableHead>{t('reports.colItem', 'Item')}</TableHead>
                  <TableHead>{t('reports.colWarehouse', 'Warehouse')}</TableHead>
                  <TableHead>{t('reports.colVoucher', 'Voucher')}</TableHead>
                  <TableHead className="text-right">{t('reports.colIn', 'In')}</TableHead>
                  <TableHead className="text-right">{t('reports.colOut', 'Out')}</TableHead>
                  <TableHead className="text-right">{t('reports.colBalanceQty', 'Balance')}</TableHead>
                  <TableHead className="text-right">{t('reports.colValue', 'Balance Value')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {kardex.data.rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8} className="h-16 text-center text-xs text-slate-400">
                      {t('reports.empty', 'Nothing open.')}
                    </TableCell>
                  </TableRow>
                ) : (
                  kardex.data.rows.map((r, i) => (
                    <TableRow key={i} className={r.isOpening ? 'bg-slate-50 font-semibold' : 'hover:bg-slate-50'}>
                      <TableCell className="font-mono text-xs">{r.postingDate}</TableCell>
                      <TableCell className="text-xs">{r.itemCode}</TableCell>
                      <TableCell className="font-mono text-xs">{r.warehouseCode}</TableCell>
                      <TableCell className="font-mono text-xs">{r.isOpening ? t('reports.opening', 'Opening') : r.voucherNo}</TableCell>
                      <TableCell className="text-right font-mono text-xs">{r.inQty ? money(r.inQty) : ''}</TableCell>
                      <TableCell className="text-right font-mono text-xs">{r.outQty ? money(r.outQty) : ''}</TableCell>
                      <TableCell className="text-right font-mono text-xs font-bold">{money(r.balanceQty)}</TableCell>
                      <TableCell className="text-right font-mono text-xs">{money(r.balanceValue)}</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          )}
        </div>
      )}
    </div>
  )
}
