import { useState } from 'react'
import { FileText, ShoppingCart, Users } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { PosCashierModal } from './PosCashierModal'
import { useNavigationStore } from '../../store/useNavigationStore'

export function SellingOverview() {
  const { t } = useTranslation('selling')
  const [isPosOpen, setIsPosOpen] = useState(false)
  const invoices = [
    { id: 'SINV-2026-0042', customer: 'Acme Corporation', date: '2026-10-01', grandTotal: '$4,500.00', outstanding: '$0.00', status: 'Paid' },
    { id: 'SINV-2026-0043', customer: 'Globex Logistics', date: '2026-10-01', grandTotal: '$1,200.00', outstanding: '$1,200.00', status: 'Unpaid' },
    { id: 'SINV-2026-0044', customer: 'Starlight Retail', date: '2026-09-30', grandTotal: '$3,800.00', outstanding: '$1,400.00', status: 'Partially Paid' },
  ]

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-emerald-100 bg-emerald-50/50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-lg font-bold text-slate-900">{t('overview.title')}</h2>
          <p className="mt-1 text-xs text-slate-600">{t('overview.subtitle')}</p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => useNavigationStore.getState().setCurrentRoute('selling-customers')}
            className="flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50"
          >
            <Users className="h-4 w-4 text-emerald-600" />
            {t('overview.manageCustomers', 'Clientes')}
          </button>
          <button
            type="button"
            className="flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50"
          >
            <FileText className="h-4 w-4 text-emerald-600" />
            {t('overview.newInvoice')}
          </button>
          <button
            type="button"
            onClick={() => setIsPosOpen(true)}
            className="flex items-center gap-1.5 rounded-lg bg-emerald-600 px-3 py-2 text-xs font-semibold text-white shadow-xs hover:bg-emerald-700"
          >
            <ShoppingCart className="h-4 w-4" />
            {t('overview.launchPos')}
          </button>
        </div>
      </div>

      {isPosOpen && <PosCashierModal onClose={() => setIsPosOpen(false)} />}

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-semibold text-slate-900">{t('invoices.title')}</h3>
        <p className="text-xs text-slate-500 mb-4">{t('invoices.subtitle')}</p>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">{t('invoices.colNumber')}</th>
                <th className="py-2.5 font-semibold">{t('invoices.colCustomer')}</th>
                <th className="py-2.5 font-semibold">{t('invoices.colDate')}</th>
                <th className="py-2.5 font-semibold text-right">{t('invoices.colTotal')}</th>
                <th className="py-2.5 font-semibold text-right">{t('invoices.colOutstanding')}</th>
                <th className="py-2.5 font-semibold text-right">{t('invoices.colStatus')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {invoices.map((inv) => (
                <tr key={inv.id} className="hover:bg-slate-50">
                  <td className="py-3 font-mono font-medium text-slate-900">{inv.id}</td>
                  <td className="py-3 text-slate-800">{inv.customer}</td>
                  <td className="py-3 font-mono text-slate-500">{inv.date}</td>
                  <td className="py-3 text-right font-mono font-bold text-slate-900">{inv.grandTotal}</td>
                  <td className="py-3 text-right font-mono text-slate-600">{inv.outstanding}</td>
                  <td className="py-3 text-right">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                        inv.status === 'Paid'
                          ? 'bg-emerald-100 text-emerald-800'
                          : inv.status === 'Partially Paid'
                            ? 'bg-amber-100 text-amber-800'
                            : 'bg-rose-100 text-rose-800'
                      }`}
                    >
                      {inv.status}
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
