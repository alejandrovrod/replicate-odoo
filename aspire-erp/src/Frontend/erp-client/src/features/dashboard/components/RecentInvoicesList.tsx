import { useTranslation } from 'react-i18next'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import type { SalesInvoice } from '../../selling/api/useSalesInvoices'

function statusClass(status: string): string {
  if (status === 'Paid') return 'bg-emerald-100 text-emerald-800'
  if (status === 'PartiallyPaid') return 'bg-amber-100 text-amber-800'
  if (status === 'Draft') return 'bg-slate-100 text-slate-700'
  return 'bg-rose-100 text-rose-800'
}

export function RecentInvoicesList({
  invoices,
  loading,
  onViewAll,
}: {
  invoices: SalesInvoice[]
  loading: boolean
  onViewAll: () => void
}) {
  const { t } = useTranslation('dashboard')
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{t('recent.title', 'Recent Invoices')}</h3>
          <p className="text-xs text-slate-500">{t('recent.subtitle', 'Latest sales invoices and their status')}</p>
        </div>
        <button
          type="button"
          onClick={onViewAll}
          className="text-xs font-semibold text-sky-600 hover:text-sky-800"
        >
          {t('recent.viewAll', 'View all →')}
        </button>
      </div>

      {loading ? (
        <div className="flex h-32 items-center justify-center text-sm text-slate-400">
          {t('recent.loading', 'Loading invoices…')}
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('recent.colNumber', 'Invoice No')}</TableHead>
              <TableHead>{t('recent.colCustomer', 'Customer')}</TableHead>
              <TableHead className="text-right">{t('recent.colTotal', 'Total')}</TableHead>
              <TableHead className="text-right">{t('recent.colStatus', 'Status')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {invoices.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} className="h-24 text-center text-slate-500">
                  {t('recent.empty', 'No invoices yet.')}
                </TableCell>
              </TableRow>
            ) : (
              invoices.slice(0, 6).map((inv) => (
                <TableRow key={inv.id} className="hover:bg-slate-50">
                  <TableCell className="font-mono font-medium text-slate-900">{inv.invoiceNumber}</TableCell>
                  <TableCell className="text-slate-800">{inv.customerName ?? ''}</TableCell>
                  <TableCell className="text-right font-mono font-bold text-slate-900">
                    {inv.grandTotal.toLocaleString()}
                  </TableCell>
                  <TableCell className="text-right">
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${statusClass(inv.status)}`}>
                      {inv.status}
                    </span>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
