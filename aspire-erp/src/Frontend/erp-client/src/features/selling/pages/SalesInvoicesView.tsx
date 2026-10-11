import { useState } from 'react'
import { ApiError } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { usePagination } from '../../../lib/pagination'
import { SalesInvoiceList } from '../components/SalesInvoiceList'
import { SalesInvoiceFormModal } from '../components/SalesInvoiceFormModal'
import { createSalesInvoice, submitSalesInvoice, useSalesInvoices, type CreateSalesInvoicePayload, type SalesInvoice } from '../api/useSalesInvoices'

export function SalesInvoicesView() {
  const companyId = useTenantStore((state) => state.companyId)
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useSalesInvoices(companyId, page, pageSize)

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [submittingId, setSubmittingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const handleSave = async (data: CreateSalesInvoicePayload) => {
    await createSalesInvoice(data)
    reload()
  }

  const handleSubmit = async (invoice: SalesInvoice) => {
    setActionError(null)
    setSubmittingId(invoice.id)
    try {
      await submitSalesInvoice(invoice.id, companyId)
      reload()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setActionError(err.message)
      } else if (err instanceof Error) {
        setActionError(err.message)
      } else {
        setActionError('Failed to submit invoice.')
      }
    } finally {
      setSubmittingId(null)
    }
  }

  if (status === 'loading' && items.length === 0) return <div className="p-4">Loading sales invoices…</div>
  if (error) return <div className="p-4 text-red-600">Error loading sales invoices: {error.message}</div>

  return (
    <div className="p-4 space-y-4">
      {actionError && <div className="text-sm text-red-600">{actionError}</div>}
      <SalesInvoiceList
        invoices={items}
        totalCount={totalCount}
        page={pageNumber}
        pageSize={pageSize}
        onPageChange={setPage}
        onNew={() => setIsModalOpen(true)}
        onSubmit={handleSubmit}
        submittingId={submittingId}
      />
      <SalesInvoiceFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        companyId={companyId}
        invoices={items}
      />
    </div>
  )
}
