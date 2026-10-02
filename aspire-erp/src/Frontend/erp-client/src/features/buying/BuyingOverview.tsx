import { FileText, Truck } from 'lucide-react'

export function BuyingOverview() {
  const purchaseOrders = [
    { id: 'PO-2026-0018', vendor: 'Global Metal Supplies', date: '2026-10-01', amount: '$8,400.00', status: 'Received & Billed' },
    { id: 'PO-2026-0019', vendor: 'Apex Packaging Ltd', date: '2026-09-29', amount: '$2,150.00', status: 'Awaiting Receipt' },
    { id: 'PO-2026-0020', vendor: 'Precision Fasteners Inc', date: '2026-09-28', amount: '$4,920.00', status: 'Received (Unbilled)' },
  ]

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-indigo-100 bg-indigo-50/50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-lg font-bold text-slate-900">Buying & Procurement Subsystem</h2>
          <p className="mt-1 text-xs text-slate-600">
            Purchase orders, material receipts, and 3-way matching with interim accruals (`Stock Received But Not Billed`).
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            className="flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50"
          >
            <Truck className="h-4 w-4 text-indigo-600" />
            Purchase Receipt
          </button>
          <button
            type="button"
            className="flex items-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-2 text-xs font-semibold text-white shadow-xs hover:bg-indigo-700"
          >
            <FileText className="h-4 w-4" />
            New Purchase Order
          </button>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-semibold text-slate-900">Procurement Orders (`PurchaseOrder`)</h3>
        <p className="text-xs text-slate-500 mb-4">Vendor commitments and interim accrual status.</p>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">PO Number</th>
                <th className="py-2.5 font-semibold">Vendor</th>
                <th className="py-2.5 font-semibold">Order Date</th>
                <th className="py-2.5 font-semibold text-right">Amount</th>
                <th className="py-2.5 font-semibold text-right">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {purchaseOrders.map((po) => (
                <tr key={po.id} className="hover:bg-slate-50">
                  <td className="py-3 font-mono font-medium text-slate-900">{po.id}</td>
                  <td className="py-3 text-slate-800">{po.vendor}</td>
                  <td className="py-3 font-mono text-slate-500">{po.date}</td>
                  <td className="py-3 text-right font-mono font-bold text-slate-900">{po.amount}</td>
                  <td className="py-3 text-right">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                        po.status === 'Received & Billed'
                          ? 'bg-emerald-100 text-emerald-800'
                          : po.status === 'Received (Unbilled)'
                            ? 'bg-amber-100 text-amber-800'
                            : 'bg-sky-100 text-sky-800'
                      }`}
                    >
                      {po.status}
                    </span>
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
