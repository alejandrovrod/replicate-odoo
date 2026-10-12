import { useTranslation } from 'react-i18next'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { Pagination } from '../../../components/ui/Pagination'
import { Button } from '../../../components/ui/Button'
import { translateDocStatus } from '../../../lib/docStatus'
import type { PaymentEntry } from '../api/usePaymentEntries'

interface PaymentEntryListProps {
  payments: PaymentEntry[]
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onNew: () => void
  onSubmit: (payment: PaymentEntry) => void
  onCancel: (payment: PaymentEntry) => void
  actingId?: string | null
}

function statusClass(status: string): string {
  if (status === 'Submitted') return 'bg-sky-100 text-sky-800'
  if (status === 'Cancelled') return 'bg-slate-200 text-slate-600'
  return 'bg-amber-100 text-amber-800'
}

export function PaymentEntryList({
  payments,
  totalCount,
  page,
  pageSize,
  onPageChange,
  onNew,
  onSubmit,
  onCancel,
  actingId,
}: PaymentEntryListProps) {
  const { t, i18n } = useTranslation('banking')
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{t('payments.title', 'Payment Entries')}</h3>
          <p className="text-xs text-slate-500">{t('payments.subtitle')}</p>
        </div>
        <Button size="sm" onClick={onNew}>
          {t('payments.new', 'New Payment')}
        </Button>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white shadow-xs">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('payments.colVoucher', 'Voucher No')}</TableHead>
              <TableHead>{t('payments.colType', 'Type')}</TableHead>
              <TableHead>{t('payments.colDate', 'Date')}</TableHead>
              <TableHead className="text-right">{t('payments.colPaid', 'Paid')}</TableHead>
              <TableHead className="text-right">{t('payments.colUnallocated', 'Unallocated')}</TableHead>
              <TableHead className="text-right">{t('payments.colStatus', 'Status')}</TableHead>
              <TableHead className="text-right">{t('payments.colActions', 'Actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {payments.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="h-24 text-center text-slate-500">
                  {t('payments.empty', 'No payment entries yet.')}
                </TableCell>
              </TableRow>
            ) : (
              payments.map((p) => (
                <TableRow key={p.id} className="hover:bg-slate-50">
                  <TableCell className="font-mono font-medium text-slate-900">
                    {p.voucherNo || <span className="text-slate-400">{t('payments.draftBadge', '— draft —')}</span>}
                  </TableCell>
                  <TableCell className="text-slate-800">
                    {p.paymentType === 'Receive'
                      ? t('payments.typeReceive', 'Receive')
                      : p.paymentType === 'Pay'
                        ? t('payments.typePay', 'Pay')
                        : p.paymentType === 'InternalTransfer'
                          ? t('payments.typeTransfer', 'Internal Transfer')
                          : p.paymentType}
                  </TableCell>
                  <TableCell className="font-mono text-slate-500">{p.paymentDate}</TableCell>
                  <TableCell className="text-right font-mono font-bold text-slate-900">
                    {p.paidAmount.toLocaleString()}
                  </TableCell>
                  <TableCell className="text-right font-mono text-slate-600">
                    {p.unallocatedAmount.toLocaleString()}
                  </TableCell>
                  <TableCell className="text-right">
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${statusClass(p.documentStatus)}`}>
                      {translateDocStatus(i18n, p.documentStatus)}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    <span className="inline-flex gap-1">
                      {p.documentStatus === 'Draft' && (
                        <Button size="sm" variant="outline" disabled={actingId === p.id} onClick={() => onSubmit(p)}>
                          {actingId === p.id ? t('payments.submitting', '…') : t('payments.submit', 'Submit')}
                        </Button>
                      )}
                      {p.documentStatus === 'Submitted' && (
                        <Button size="sm" variant="outline" disabled={actingId === p.id} onClick={() => onCancel(p)}>
                          {actingId === p.id ? t('payments.submitting', '…') : t('payments.cancel', 'Cancel')}
                        </Button>
                      )}
                    </span>
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
