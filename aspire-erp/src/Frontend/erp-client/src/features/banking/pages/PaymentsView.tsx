import { useState } from 'react'
import { ApiError } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { usePagination } from '../../../lib/pagination'
import { PaymentEntryList } from '../components/PaymentEntryList'
import { PaymentEntryModal } from '../components/PaymentEntryModal'
import {
  cancelPaymentEntry,
  createPaymentEntry,
  submitPaymentEntry,
  usePaymentEntries,
  type PaymentEntry,
} from '../api/usePaymentEntries'

export function PaymentsView() {
  const companyId = useTenantStore((state) => state.companyId)
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = usePaymentEntries(companyId, page, pageSize)

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [actingId, setActingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

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
        setActionError(err.message)
      } else if (err instanceof Error) {
        setActionError(err.message)
      } else {
        setActionError(`Failed to ${action} payment.`)
      }
    } finally {
      setActingId(null)
    }
  }

  if (status === 'loading' && items.length === 0) return <div className="p-4">Loading payments…</div>
  if (error) return <div className="p-4 text-red-600">Error loading payments: {error.message}</div>

  return (
    <div className="p-4 space-y-4">
      {actionError && <div className="text-sm text-red-600">{actionError}</div>}
      <PaymentEntryList
        payments={items}
        totalCount={totalCount}
        page={pageNumber}
        pageSize={pageSize}
        onPageChange={setPage}
        onNew={() => setIsModalOpen(true)}
        onSubmit={(p) => runAction(p, 'submit')}
        onCancel={(p) => runAction(p, 'cancel')}
        actingId={actingId}
      />
      <PaymentEntryModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        companyId={companyId}
      />
    </div>
  )
}
