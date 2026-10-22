import { useTranslation } from 'react-i18next'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { Pagination } from '../../../components/ui/Pagination'
import { Button } from '../../../components/ui/Button'
import { FloatingActionButton } from '../../../components/ui/FloatingActionButton'
import { translateDocStatus } from '../../../lib/docStatus'
import type { SalesInvoice } from '../api/useSalesInvoices'

interface SalesInvoiceListProps {
  invoices: SalesInvoice[]
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onNew: () => void
  onSubmit: (invoice: SalesInvoice) => void
  submittingId?: string | null
}

function statusClass(status: string): string {
  if (status === 'Paid') return 'bg-emerald-100 text-emerald-800'
  if (status === 'PartiallyPaid') return 'bg-amber-100 text-amber-800'
  if (status === 'Unpaid') return 'bg-rose-100 text-rose-800'
  return 'bg-slate-100 text-slate-700'
}

export function SalesInvoiceList({
  invoices,
  totalCount,
  page,
  pageSize,
  onPageChange,
  onNew,
  onSubmit,
  submittingId,
}: SalesInvoiceListProps) {
  const { t, i18n } = useTranslation('selling')
  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-base font-semibold text-slate-900">{t('invoices.title')}</h3>
        <p className="text-xs text-slate-500">{t('invoices.listSubtitle', 'Draft invoices submit to Unpaid (A/R + revenue).')}</p>
      </div>

      <FloatingActionButton label={t('invoices.new', 'New Invoice')} onClick={onNew} />

      <div className="rounded-xl border border-slate-200 bg-white shadow-xs">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('invoices.colNumber')}</TableHead>
              <TableHead>{t('invoices.colCustomer')}</TableHead>
              <TableHead>{t('invoices.colDate')}</TableHead>
              <TableHead className="text-right">{t('invoices.colTotal')}</TableHead>
              <TableHead className="text-right">{t('invoices.colOutstanding')}</TableHead>
              <TableHead className="text-right">{t('invoices.colStatus')}</TableHead>
              <TableHead className="text-right">{t('orders.colAction', 'Action')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {invoices.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="h-24 text-center text-slate-500">
                  {t('invoices.empty', 'No sales invoices yet.')}
                </TableCell>
              </TableRow>
            ) : (
              invoices.map((inv) => (
                <TableRow key={inv.id} className="hover:bg-slate-50">
                  <TableCell className="font-mono font-medium text-slate-900">
                    {inv.invoiceNumber}
                    {inv.isReturn && (
                      <span className="ml-1.5 rounded-full bg-violet-100 px-2 py-0.5 text-[10px] font-semibold text-violet-800">
                        {t('invoices.creditNote', 'CN')}
                      </span>
                    )}
                    {inv.updateStock && (
                      <span className="ml-1.5 rounded-full bg-sky-100 px-2 py-0.5 text-[10px] font-semibold text-sky-800">
                        {t('invoices.directDelivery', 'Direct delivery')}
                      </span>
                    )}
                    {(inv.totalAdvance ?? 0) > 0 && (
                      <span className="ml-1.5 rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-800">
                        {t('invoices.advanceApplied', 'Advance')}
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-slate-800">{inv.customerName ?? ''}</TableCell>
                  <TableCell className="font-mono text-slate-500">{inv.postingDate}</TableCell>
                  <TableCell
                    className={`text-right font-mono font-bold ${inv.grandTotal < 0 ? 'text-rose-700' : 'text-slate-900'}`}
                  >
                    {inv.grandTotal.toLocaleString()}
                  </TableCell>
                  <TableCell className="text-right font-mono text-slate-600">
                    {inv.outstandingAmount.toLocaleString()}
                  </TableCell>
                  <TableCell className="text-right">
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${statusClass(inv.status)}`}>
                      {translateDocStatus(i18n, inv.status)}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    {inv.status === 'Draft' && (
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={submittingId === inv.id}
                        onClick={() => onSubmit(inv)}
                      >
                        {submittingId === inv.id ? t('invoices.submitting', 'Submitting…') : t('invoices.submit', 'Submit')}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {totalCount > 0 && (
        <div className="flex justify-end">
          <Pagination totalCount={totalCount} page={page} pageSize={pageSize} onPageChange={onPageChange} />
        </div>
      )}
    </div>
  )
}
