import { CheckCircle2 } from 'lucide-react'

export function GeneralLedgerOverview() {
  const glEntries = [
    { id: 1001, date: '2026-10-01', account: '1110 - Cash and Cash Equivalents', debit: '$5,400.00', credit: '$0.00', voucher: 'PAY-2026-0001', party: 'Stripe Inc' },
    { id: 1002, date: '2026-10-01', account: '1120 - Accounts Receivable', debit: '$0.00', credit: '$5,400.00', voucher: 'PAY-2026-0001', party: 'Stripe Inc' },
    { id: 1003, date: '2026-09-30', account: '5110 - Office Supplies Expense', debit: '$450.20', credit: '$0.00', voucher: 'JV-2026-0014', party: 'AWS Services' },
    { id: 1004, date: '2026-09-30', account: '1110 - Cash and Cash Equivalents', debit: '$0.00', credit: '$450.20', voucher: 'JV-2026-0014', party: 'AWS Services' },
  ]

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-sky-50/50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-lg font-bold text-slate-900">General Ledger (`GLEntry`)</h2>
          <p className="mt-1 text-xs text-slate-600">
            Immutable, append-only double-entry financial ledger. Enforces invariant &Sigma; Debit = &Sigma; Credit.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <span className="flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800">
            <CheckCircle2 className="h-4 w-4" /> &Sigma;D = &Sigma;C Verified (0.00 Diff)
          </span>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">Entry #</th>
                <th className="py-2.5 font-semibold">Posting Date</th>
                <th className="py-2.5 font-semibold">Account</th>
                <th className="py-2.5 font-semibold">Voucher</th>
                <th className="py-2.5 font-semibold">Party</th>
                <th className="py-2.5 font-semibold text-right">Debit</th>
                <th className="py-2.5 font-semibold text-right">Credit</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {glEntries.map((row) => (
                <tr key={row.id} className="hover:bg-slate-50">
                  <td className="py-3 font-mono text-slate-400">{row.id}</td>
                  <td className="py-3 font-mono text-slate-600">{row.date}</td>
                  <td className="py-3 font-medium text-slate-900">{row.account}</td>
                  <td className="py-3 font-mono text-sky-600">{row.voucher}</td>
                  <td className="py-3 text-slate-600">{row.party}</td>
                  <td className="py-3 text-right font-mono font-medium text-slate-900">{row.debit}</td>
                  <td className="py-3 text-right font-mono font-medium text-slate-900">{row.credit}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t-2 border-slate-300 font-bold">
                <td colSpan={5} className="py-3 text-right text-slate-700">Total Movements:</td>
                <td className="py-3 text-right font-mono text-slate-900">$5,850.20</td>
                <td className="py-3 text-right font-mono text-slate-900">$5,850.20</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>
    </div>
  )
}
