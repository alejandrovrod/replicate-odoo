import { useState } from 'react'
import { ApiError } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { usePagination } from '../../../lib/pagination'
import { SalesOrderList } from '../components/SalesOrderList'
import { SalesOrderFormModal } from '../components/SalesOrderFormModal'
import { createSalesOrder, submitSalesOrder, useSalesOrders, type SalesOrder } from '../api/useSalesOrders'

export function SalesOrdersView() {
  const companyId = useTenantStore((state) => state.companyId)
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useSalesOrders(companyId, page, pageSize)

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [submittingId, setSubmittingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const handleSave = async (data: {
    customerId: string
    transactionDate: string
    deliveryDate: string
    lines: { itemId: string; quantity: number; rate: number }[]
  }) => {
    await createSalesOrder({ companyId, ...data })
    reload()
  }

  const handleSubmit = async (order: SalesOrder) => {
    setActionError(null)
    setSubmittingId(order.id)
    try {
      await submitSalesOrder(order.id, companyId)
      reload()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setActionError(err.message)
      } else if (err instanceof Error) {
        setActionError(err.message)
      } else {
        setActionError('Failed to submit order.')
      }
    } finally {
      setSubmittingId(null)
    }
  }

  if (status === 'loading' && items.length === 0) return <div className="p-4">Loading sales orders…</div>
  if (error) return <div className="p-4 text-red-600">Error loading sales orders: {error.message}</div>

  return (
    <div className="p-4 space-y-4">
      {actionError && <div className="text-sm text-red-600">{actionError}</div>}
      <SalesOrderList
        orders={items}
        totalCount={totalCount}
        page={pageNumber}
        pageSize={pageSize}
        onPageChange={setPage}
        onNew={() => setIsModalOpen(true)}
        onSubmit={handleSubmit}
        submittingId={submittingId}
      />
      <SalesOrderFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        companyId={companyId}
      />
    </div>
  )
}
