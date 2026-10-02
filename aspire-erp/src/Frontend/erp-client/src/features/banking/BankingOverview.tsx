import {
  ArrowDownLeft,
  ArrowUpRight,
  CheckCircle2,
  Landmark,
  PlusCircle,
  RefreshCw,
  Sparkles,
  UploadCloud,
} from 'lucide-react'

export function BankingOverview() {
  const bankAccounts = [
    {
      name: 'Main Operating Account (USD)',
      bank: 'JPMorgan Chase',
      accountNo: '•••• 4920',
      glAccount: '1110 - Cash & Equivalents',
      balance: '$62,450.00',
      unreconciledCount: 8,
      status: 'Action Needed',
    },
    {
      name: 'Stripe Settlement Account',
      bank: 'Stripe Gateway',
      accountNo: 'ACCT-STRIPE-01',
      glAccount: '1115 - Payment Gateway Clearing',
      balance: '$21,870.50',
      unreconciledCount: 0,
      status: 'Reconciled',
    },
  ]

  const stagingLines = [
    {
      id: 'tx-1',
      date: '2026-10-01',
      description: 'STRIPE PAYOUT REF #98234',
      type: 'Deposit',
      amount: '$5,400.00',
      status: 'Matched',
      ruleMatch: 'Auto-Matched: Rule #1 (Stripe)',
    },
    {
      id: 'tx-2',
      date: '2026-09-30',
      description: 'ACH DEBIT AWS CLOUD SERVICES',
      type: 'Withdrawal',
      amount: '$450.20',
      status: 'Unreconciled',
      ruleMatch: 'Requires Voucher Creation',
    },
    {
      id: 'tx-3',
      date: '2026-09-29',
      description: 'WIRE TRANSFER INVOICE #SINV-2026-0012',
      type: 'Deposit',
      amount: '$12,500.00',
      status: 'Matched',
      ruleMatch: 'Matched: Customer Acme Corp',
    },
  ]

  return (
    <div className="space-y-6">
      {/* Top Banner & Actions */}
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-gradient-to-r from-sky-50 to-indigo-50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-sky-600 text-white shadow-xs">
              <Landmark className="h-4 w-4" />
            </span>
            <h2 className="text-lg font-bold text-slate-900">Banking Subsystem (ERPNext Parity)</h2>
          </div>
          <p className="mt-1 text-xs text-slate-600">
            Import bank statements, run automated heuristic matching rules, and reconcile transactions with GL vouchers.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50"
          >
            <UploadCloud className="h-4 w-4 text-sky-600" />
            Import Statement (CSV/OFX)
          </button>
          <button
            type="button"
            className="flex items-center gap-1.5 rounded-lg bg-sky-600 px-3 py-2 text-xs font-semibold text-white shadow-xs hover:bg-sky-700"
          >
            <Sparkles className="h-4 w-4" />
            Run Rules Engine
          </button>
        </div>
      </div>

      {/* Bank Account Cards */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        {bankAccounts.map((acc) => (
          <div key={acc.name} className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
            <div className="flex items-start justify-between">
              <div>
                <h3 className="font-semibold text-slate-900">{acc.name}</h3>
                <p className="text-xs text-slate-500">
                  {acc.bank} · <span className="font-mono">{acc.accountNo}</span>
                </p>
                <p className="mt-1 text-[11px] font-medium text-slate-400">{acc.glAccount}</p>
              </div>
              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                  acc.unreconciledCount === 0
                    ? 'bg-emerald-100 text-emerald-800'
                    : 'bg-amber-100 text-amber-800'
                }`}
              >
                {acc.unreconciledCount === 0 ? 'Fully Reconciled' : `${acc.unreconciledCount} Unmatched`}
              </span>
            </div>

            <div className="mt-5 flex items-baseline justify-between border-t border-slate-100 pt-4">
              <div>
                <span className="text-xs text-slate-400">Ledger Balance</span>
                <p className="text-xl font-bold text-slate-900">{acc.balance}</p>
              </div>
              <button
                type="button"
                className="flex items-center gap-1 text-xs font-semibold text-sky-600 hover:text-sky-700"
              >
                <RefreshCw className="h-3.5 w-3.5" />
                Reconcile Now
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Staging Bank Transactions Table */}
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <div className="flex items-center justify-between border-b border-slate-100 pb-4">
          <div>
            <h3 className="text-base font-semibold text-slate-900">
              Bank Transaction Staging (`BankTransaction`)
            </h3>
            <p className="text-xs text-slate-500">
              Isolated staging area. Invariant: zero accounting entries are posted until reconciliation.
            </p>
          </div>
          <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700">
            <CheckCircle2 className="h-4 w-4" /> Staging Isolation Active
          </span>
        </div>

        <div className="mt-4 overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">Date</th>
                <th className="py-2.5 font-semibold">Statement Description</th>
                <th className="py-2.5 font-semibold">Type</th>
                <th className="py-2.5 font-semibold text-right">Amount</th>
                <th className="py-2.5 font-semibold">Rule Evaluation</th>
                <th className="py-2.5 font-semibold text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {stagingLines.map((line) => (
                <tr key={line.id} className="hover:bg-slate-50">
                  <td className="py-3 font-mono text-slate-600">{line.date}</td>
                  <td className="py-3 font-medium text-slate-900">{line.description}</td>
                  <td className="py-3">
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                        line.type === 'Deposit'
                          ? 'bg-emerald-50 text-emerald-700'
                          : 'bg-rose-50 text-rose-700'
                      }`}
                    >
                      {line.type === 'Deposit' ? (
                        <ArrowDownLeft className="h-3 w-3" />
                      ) : (
                        <ArrowUpRight className="h-3 w-3" />
                      )}
                      {line.type}
                    </span>
                  </td>
                  <td className="py-3 text-right font-mono font-bold text-slate-900">{line.amount}</td>
                  <td className="py-3">
                    <span className="rounded bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-700">
                      {line.ruleMatch}
                    </span>
                  </td>
                  <td className="py-3 text-right">
                    {line.status === 'Matched' ? (
                      <button
                        type="button"
                        className="rounded bg-sky-50 px-2.5 py-1 text-xs font-semibold text-sky-700 hover:bg-sky-100"
                      >
                        Confirm Match
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="flex items-center gap-1 rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-200 ml-auto"
                      >
                        <PlusCircle className="h-3 w-3" />
                        Quick Voucher
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
