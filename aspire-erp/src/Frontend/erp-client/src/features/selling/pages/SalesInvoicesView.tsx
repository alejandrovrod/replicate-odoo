import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { usePagination } from '../../../lib/pagination'
import { SalesInvoiceList } from '../components/SalesInvoiceList'
import { SalesInvoiceFormModal } from '../components/SalesInvoiceFormModal'
import { createSalesInvoice, submitSalesInvoice, useSalesInvoices, type CreateSalesInvoicePayload, type SalesInvoice } from '../api/useSalesInvoices'

export function SalesInvoicesView() {
  const { t } = useTranslation('selling')
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
        setActionError(t('invoices.submitError', 'Failed to submit invoice.'))
      }
    } finally {
      setSubmittingId(null)
    }
  }

  if (status === 'loading' && items.length === 0) return <div className="p-4">{t('invoices.loading', 'Loading sales invoices…')}</div>
  if (error) return <div className="p-4 text-red-600">{t('invoices.loadError', 'Error loading sales invoices: {{message}}', { message: error.message })}</div>

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
