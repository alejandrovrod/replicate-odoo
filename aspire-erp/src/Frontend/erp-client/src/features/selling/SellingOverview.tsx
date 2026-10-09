import { useState } from 'react'
import { FileText, ShoppingCart, Users, ClipboardList } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { PosCashierModal } from './PosCashierModal'
import { useNavigationStore } from '../../store/useNavigationStore'
import { useTenantStore } from '../../store/useTenantStore'
import { Button } from '../../components/ui/Button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/Table'
import { useSalesInvoices } from './api/useSalesInvoices'

export function SellingOverview() {
  const { t } = useTranslation('selling')
  const [isPosOpen, setIsPosOpen] = useState(false)
  const companyId = useTenantStore((state) => state.companyId)
  const { items, status, error } = useSalesInvoices(companyId, 1, 5)
  const navigate = useNavigationStore((state) => state.setCurrentRoute)

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-sky-100 bg-gradient-to-r from-sky-50 to-white p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-lg font-bold text-slate-900">{t('overview.title')}</h2>
          <p className="mt-1 text-xs text-slate-600">{t('overview.subtitle')}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => navigate('selling-customers')} className="flex items-center gap-1.5">
            <Users className="h-4 w-4" />
            {t('overview.manageCustomers', 'Clientes')}
          </Button>
          <Button variant="secondary" size="sm" onClick={() => navigate('selling-orders')} className="flex items-center gap-1.5">
            <ClipboardList className="h-4 w-4" />
            {t('overview.orders', 'Orders')}
          </Button>
          <Button variant="secondary" size="sm" onClick={() => navigate('selling-invoices')} className="flex items-center gap-1.5">
            <FileText className="h-4 w-4" />
            {t('overview.newInvoice')}
          </Button>
          <Button variant="default" size="sm" onClick={() => setIsPosOpen(true)} className="flex items-center gap-1.5">
            <ShoppingCart className="h-4 w-4" />
            {t('overview.launchPos')}
          </Button>
        </div>
      </div>

      {isPosOpen && <PosCashierModal onClose={() => setIsPosOpen(false)} />}

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-semibold text-slate-900">{t('invoices.title')}</h3>
        <p className="text-xs text-slate-500 mb-4">{t('invoices.subtitle')}</p>

        {status === 'loading' && <div className="text-xs text-slate-500">Loading…</div>}
        {error && <div className="text-xs text-red-600">Error: {error.message}</div>}
        {status !== 'loading' && !error && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('invoices.colNumber')}</TableHead>
                <TableHead>{t('invoices.colCustomer')}</TableHead>
                <TableHead>{t('invoices.colDate')}</TableHead>
                <TableHead className="text-right">{t('invoices.colTotal')}</TableHead>
                <TableHead className="text-right">{t('invoices.colOutstanding')}</TableHead>
                <TableHead className="text-right">{t('invoices.colStatus')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="h-24 text-center text-slate-500">
                    No invoices yet — create one from Invoices.
                  </TableCell>
                </TableRow>
              ) : (
                items.map((inv) => (
                  <TableRow key={inv.id} className="hover:bg-slate-50">
                    <TableCell className="font-mono font-medium text-slate-900">{inv.invoiceNumber}</TableCell>
                    <TableCell className="text-slate-800">{inv.customerName ?? ''}</TableCell>
                    <TableCell className="font-mono text-slate-500">{inv.postingDate}</TableCell>
                    <TableCell className="text-right font-mono font-bold text-slate-900">
                      {inv.grandTotal.toLocaleString()}
                    </TableCell>
                    <TableCell className="text-right font-mono text-slate-600">
                      {inv.outstandingAmount.toLocaleString()}
                    </TableCell>
                    <TableCell className="text-right">
                      <span
                        className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                          inv.status === 'Paid'
                            ? 'bg-emerald-100 text-emerald-800'
                            : inv.status === 'PartiallyPaid'
                              ? 'bg-amber-100 text-amber-800'
                              : inv.status === 'Draft'
                                ? 'bg-slate-100 text-slate-700'
                                : 'bg-rose-100 text-rose-800'
                        }`}
                      >
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
    </div>
  )
}
