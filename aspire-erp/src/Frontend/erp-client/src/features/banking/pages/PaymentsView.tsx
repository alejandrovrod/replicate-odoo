import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useTenantStore } from '../../../store/useTenantStore'
import { usePagination } from '../../../lib/pagination'
import { PaymentEntryList, type PaymentListFilters } from '../components/PaymentEntryList'
import { PaymentEntryModal } from '../components/PaymentEntryModal'
import { PaymentEntryDetailDialog } from '../components/PaymentEntryDetailDialog'
import {
  cancelPaymentEntry,
  createPaymentEntry,
  submitPaymentEntry,
  usePaymentEntries,
  type PaymentEntry,
} from '../api/usePaymentEntries'

const EMPTY_FILTERS: PaymentListFilters = { status: '', paymentType: '', text: '' }

/** Rows the free-text box matches, over the fields the operator can actually see. */
function matchesText(payment: PaymentEntry, needle: string): boolean {
  return [
    payment.voucherNo,
    payment.partyName,
    payment.paymentDate,
    payment.referenceNumber,
    payment.paymentType,
    payment.documentStatus,
  ].some((value) => (value ?? '').toString().toLowerCase().includes(needle))
}

export function PaymentsView() {
  const { t, i18n } = useTranslation('banking')
  const companyId = useTenantStore((state) => state.companyId)
  const { page, pageSize, setPage, reset } = usePagination(10)

  const [filters, setFilters] = useState<PaymentListFilters>(EMPTY_FILTERS)
  const { items, status, error, reload, totalCount, pageNumber } = usePaymentEntries(
    companyId,
    page,
    pageSize,
    { status: filters.status, paymentType: filters.paymentType },
  )

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [detailId, setDetailId] = useState<string | null>(null)
  const [actingId, setActingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  // Server-side filters (status/type) narrow the page request; the free-text box narrows the
  // loaded page in place, which is why the hint says so instead of implying a global search.
  const visibleItems = useMemo(() => {
    const needle = filters.text.trim().toLowerCase()
    if (!needle) return items
    return items.filter((payment) => matchesText(payment, needle))
  }, [items, filters.text])

  const handleFilterChange = (patch: Partial<PaymentListFilters>) => {
    setFilters((prev) => ({ ...prev, ...patch }))
    reset()
  }

  const handleSave = async (data: {
    paymentType: string
    partyType: string
    partyId: string
    bankAccountId: string
    paymentDate: string
    paidAmount: number
    referenceNumber?: string | null
    allocations: { salesInvoiceId?: string | null; purchaseInvoiceId?: string | null; allocatedAmount: number }[]
    partyName?: string
    modeOfPayment?: string
    remarks?: string
  }) => {
    await createPaymentEntry({ companyId, ...data })
    reload()
  }

  const runAction = async (payment: PaymentEntry, action: 'submit' | 'cancel') => {
    setActionError(null)
    setActingId(payment.id)
    try {
      if (action === 'submit') {
        await submitPaymentEntry(payment.id, companyId, payment.rowVersion)
      } else {
        await cancelPaymentEntry(payment.id, companyId, payment.rowVersion)
      }
      reload()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setActionError(translateErrorCode(i18n, err.code, err.message))
      } else if (err instanceof Error) {
        setActionError(err.message)
      } else {
        setActionError(
          action === 'submit'
            ? t('payments.submitError', 'Failed to submit payment.')
            : t('payments.cancelError', 'Failed to cancel payment.'),
        )
      }
    } finally {
      setActingId(null)
    }
  }

  if (status === 'loading' && items.length === 0) return <div className="p-4">{t('payments.loading', 'Loading payments…')}</div>
  if (error) return <div className="p-4 text-red-600">{t('payments.loadError', 'Error loading payments: {{message}}', { message: error.message })}</div>

  return (
    <div className="p-4 space-y-4">
      {actionError && <div className="text-sm text-red-600">{actionError}</div>}
      <PaymentEntryList
        payments={visibleItems}
        totalCount={totalCount}
        page={pageNumber}
        pageSize={pageSize}
        onPageChange={setPage}
        onNew={() => setIsModalOpen(true)}
        onSubmit={(p) => runAction(p, 'submit')}
        onCancel={(p) => runAction(p, 'cancel')}
        onRowClick={(p) => setDetailId(p.id)}
        filters={filters}
        onFilterChange={handleFilterChange}
        actingId={actingId}
      />
      <PaymentEntryModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        companyId={companyId}
      />
      <PaymentEntryDetailDialog
        isOpen={detailId !== null}
        paymentId={detailId}
        companyId={companyId}
        onClose={() => setDetailId(null)}
      />
    </div>
  )
}
