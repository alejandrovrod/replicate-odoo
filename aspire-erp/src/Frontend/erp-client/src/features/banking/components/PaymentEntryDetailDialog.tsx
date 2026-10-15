import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/Dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../components/ui/Tabs'
import { Button } from '../../../components/ui/Button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { translateDocStatus } from '../../../lib/docStatus'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { formatDelta, formatMoney } from '../../../lib/format'
import {
  usePaymentDetail,
  type GlEntry,
  type PaymentAllocation,
  type PaymentEntry,
} from '../api/usePaymentEntries'

interface PaymentEntryDetailDialogProps {
  isOpen: boolean
  paymentId: string | null
  companyId: string
  onClose: () => void
}

/** Same tolerance as `DoubleEntryImbalanceException` (|ΣD - ΣC| <= 0.0001). */
const BALANCE_TOLERANCE = 0.0001

function statusClass(status: string): string {
  if (status === 'Submitted') return 'bg-sky-100 text-sky-800'
  if (status === 'Cancelled') return 'bg-slate-200 text-slate-600'
  return 'bg-amber-100 text-amber-800'
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1">
      <dt className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">{label}</dt>
      <dd className="text-sm text-slate-900">{children}</dd>
    </div>
  )
}

/** The invoice id a slice settled, whatever side of the ledger it came from. */
function allocationReference(allocation: PaymentAllocation): string {
  return (
    allocation.referenceDocumentId ??
    allocation.salesInvoiceId ??
    allocation.purchaseInvoiceId ??
    '—'
  )
}

function GlTable({ entries }: { entries: GlEntry[] }) {
  const { t } = useTranslation('banking')
  const totalDebit = entries.reduce((sum, entry) => sum + entry.debit, 0)
  const totalCredit = entries.reduce((sum, entry) => sum + entry.credit, 0)
  const balanced = Math.abs(totalDebit - totalCredit) <= BALANCE_TOLERANCE

  if (entries.length === 0) {
    return (
      <p className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-500">
        {t('payments.glEmpty', 'No ledger lines yet — the voucher is still a draft.')}
      </p>
    )
  }

  return (
    <div className="rounded-lg border border-slate-200">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('payments.glColDate', 'Date')}</TableHead>
            <TableHead>{t('payments.glColAccount', 'Account')}</TableHead>
            <TableHead className="text-right">{t('payments.glColDebit', 'Debit')}</TableHead>
            <TableHead className="text-right">{t('payments.glColCredit', 'Credit')}</TableHead>
            <TableHead>{t('payments.glColRemarks', 'Remarks')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entries.map((entry) => (
            <TableRow key={entry.id}>
              <TableCell className="font-mono text-xs text-slate-500">{entry.postingDate}</TableCell>
              <TableCell className="text-slate-900">
                <span className="font-mono text-xs text-slate-600">{entry.accountCode}</span>
                <span className="ml-2">{entry.accountName}</span>
              </TableCell>
              <TableCell className="text-right font-mono">{entry.debit ? formatMoney(entry.debit) : '—'}</TableCell>
              <TableCell className="text-right font-mono">{entry.credit ? formatMoney(entry.credit) : '—'}</TableCell>
              <TableCell className="max-w-[220px] truncate text-slate-600" title={entry.remarks ?? undefined}>
                {entry.remarks ?? '—'}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <div className="flex flex-wrap items-center justify-between gap-2 border-t-2 border-slate-300 px-4 py-2">
        <span
          className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${
            balanced ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
          }`}
        >
          {balanced
            ? t('payments.glBalanced', 'Balanced')
            : t('payments.glOutOfBalance', 'Out of balance by {{amount}}', {
                amount: formatDelta(totalDebit - totalCredit),
              })}
        </span>
        <div className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-600">
          <span>
            {t('payments.glTotalDebits', 'Total debits')}{' '}
            <strong className="font-mono text-slate-900">{formatMoney(totalDebit)}</strong>
          </span>
          <span>
            {t('payments.glTotalCredits', 'Total credits')}{' '}
            <strong className="font-mono text-slate-900">{formatMoney(totalCredit)}</strong>
          </span>
        </div>
      </div>
    </div>
  )
}

function paymentTypeLabel(payment: PaymentEntry, t: (key: string, fallback: string) => string): string {
  if (payment.paymentType === 'Receive') return t('payments.typeReceive', 'Receive')
  if (payment.paymentType === 'Pay') return t('payments.typePay', 'Pay')
  if (payment.paymentType === 'InternalTransfer') return t('payments.typeTransfer', 'Internal Transfer')
  return payment.paymentType
}

/**
 * Read-only inspection of one payment voucher (ui-spec r-12 D1 / task T1): the header values,
 * the invoices it settled, and the General Ledger lines it posted. The GL tab is meaningful
 * only after submit - a Draft has no rows, and the tab says so instead of rendering empty.
 */
export function PaymentEntryDetailDialog({
  isOpen,
  paymentId,
  companyId,
  onClose,
}: PaymentEntryDetailDialogProps) {
  const { t, i18n } = useTranslation('banking')
  const { detail, status, error, reload } = usePaymentDetail(isOpen ? paymentId : null, companyId)

  const payment = detail?.payment
  const allocations = detail?.allocations ?? []
  const glEntries = detail?.glEntries ?? []

  const title = payment?.voucherNo
    ? t('payments.detailTitle', 'Payment Entry {{voucher}}', { voucher: payment.voucherNo })
    : t('payments.detailTitleDraft', 'Payment Entry (draft)')

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) onClose() }}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>

        {status === 'loading' ? (
          <p className="py-6 text-sm text-slate-500">{t('payments.detailLoading', 'Loading payment detail…')}</p>
        ) : status === 'error' ? (
          <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
            <p className="font-medium">{t('payments.detailLoadError', 'Could not load the payment detail.')}</p>
            {error?.message ? <p className="mt-1">{translateErrorCode(i18n, error.code, error.message)}</p> : null}
            <Button size="sm" variant="outline" className="mt-2" onClick={reload}>
              {t('payments.detailRetry', 'Retry')}
            </Button>
          </div>
        ) : payment ? (
          <Tabs defaultValue="general" className="flex min-h-0 flex-col">
            <TabsList className="self-start">
              <TabsTrigger value="general">{t('payments.tabGeneral', 'General')}</TabsTrigger>
              <TabsTrigger value="allocations">{t('payments.tabAllocations', 'Allocated Invoices')}</TabsTrigger>
              <TabsTrigger value="gl">{t('payments.tabGl', 'Accounting Entry')}</TabsTrigger>
            </TabsList>

            <TabsContent value="general" className="max-h-[55vh] overflow-y-auto pr-1">
              <dl className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                <Field label={t('payments.fieldVoucher', 'Voucher No')}>
                  <span className="font-mono">
                    {payment.voucherNo || <em className="text-slate-400">{t('payments.draftBadge', '— draft —')}</em>}
                  </span>
                </Field>
                <Field label={t('payments.fieldStatus', 'Status')}>
                  <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${statusClass(payment.documentStatus)}`}>
                    {translateDocStatus(i18n, payment.documentStatus)}
                  </span>
                </Field>
                <Field label={t('payments.fieldType', 'Type')}>{paymentTypeLabel(payment, t)}</Field>
                <Field label={t('payments.fieldParty', 'Party')}>{payment.partyName || payment.partyId}</Field>
                <Field label={t('payments.fieldDate', 'Payment Date')}>
                  <span className="font-mono">{payment.paymentDate}</span>
                </Field>
                <Field label={t('payments.fieldPaidAmount', 'Paid Amount')}>
                  <span className="font-mono font-semibold">{formatMoney(payment.paidAmount)}</span>
                </Field>
                <Field label={t('payments.fieldUnallocated', 'Unallocated')}>
                  <span className={`font-mono ${payment.unallocatedAmount > 0 ? 'font-semibold text-amber-700' : ''}`}>
                    {formatMoney(payment.unallocatedAmount)}
                  </span>
                </Field>
                <Field label={t('payments.fieldReference', 'Reference No')}>{payment.referenceNumber || '—'}</Field>
                <Field label={t('payments.fieldMode', 'Mode of Payment')}>{payment.modeOfPayment || '—'}</Field>
                <Field label={t('payments.fieldClearanceDate', 'Clearance Date')}>
                  <span className="font-mono">{payment.clearanceDate || '—'}</span>
                </Field>
                <div className="sm:col-span-3">
                  <Field label={t('payments.fieldRemarks', 'Remarks')}>{payment.remarks || '—'}</Field>
                </div>
              </dl>
              {payment.unallocatedAmount > 0 ? (
                <p className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                  {t('payments.advanceNotice', 'The unallocated balance is booked as an advance to the party.')}
                </p>
              ) : null}
            </TabsContent>

            <TabsContent value="allocations" className="max-h-[55vh] overflow-y-auto pr-1">
              {allocations.length === 0 ? (
                <p className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-500">
                  {t('payments.allocEmpty', 'No invoices allocated to this voucher.')}
                </p>
              ) : (
                <div className="rounded-lg border border-slate-200">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>{t('payments.allocColReference', 'Reference')}</TableHead>
                        <TableHead>{t('payments.allocColInvoice', 'Invoice Id')}</TableHead>
                        <TableHead className="text-right">{t('payments.allocColAmount', 'Allocated')}</TableHead>
                        <TableHead className="text-right">{t('payments.allocColOutstanding', 'Outstanding')}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {allocations.map((allocation) => (
                        <TableRow key={allocation.id}>
                          <TableCell className="text-slate-800">{allocation.referenceDocumentType || '—'}</TableCell>
                          <TableCell className="font-mono text-xs text-slate-500">
                            {allocationReference(allocation)}
                          </TableCell>
                          <TableCell className="text-right font-mono font-semibold">
                            {formatMoney(allocation.allocatedAmount)}
                          </TableCell>
                          <TableCell className="text-right font-mono text-slate-600">
                            {formatMoney(allocation.outstandingAmount ?? 0)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </TabsContent>

            <TabsContent value="gl" className="max-h-[55vh] overflow-y-auto pr-1">
              <GlTable entries={glEntries} />
            </TabsContent>
          </Tabs>
        ) : (
          <p className="py-6 text-sm text-slate-500">{t('payments.detailNoData', 'No detail available.')}</p>
        )}
      </DialogContent>
    </Dialog>
  )
}
