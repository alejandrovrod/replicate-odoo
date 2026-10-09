import { useState } from 'react'
import { ApiError } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { usePagination } from '../../../lib/pagination'
import { DeliveryNoteList } from '../components/DeliveryNoteList'
import { DeliveryNoteFormModal } from '../components/DeliveryNoteFormModal'
import { postDeliveryNote, useDeliveryNotes } from '../api/useDeliveryNotes'

export function DeliveryNotesView() {
  const companyId = useTenantStore((state) => state.companyId)
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useDeliveryNotes(companyId, page, pageSize)

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  const handleSave = async (data: {
    salesOrderId: string
    warehouseId: string
    postingDate: string
    lines: { salesOrderItemId: string; itemId: string; qty: number }[]
  }) => {
    try {
      await postDeliveryNote({ companyId, ...data })
      reload()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setActionError(err.message)
      } else if (err instanceof Error) {
        setActionError(err.message)
      } else {
        setActionError('Failed to post delivery note.')
      }
      throw err
    }
  }

  if (status === 'loading' && items.length === 0) return <div className="p-4">Loading delivery notes…</div>
  if (error) return <div className="p-4 text-red-600">Error loading delivery notes: {error.message}</div>

  return (
    <div className="p-4 space-y-4">
      {actionError && <div className="text-sm text-red-600">{actionError}</div>}
      <DeliveryNoteList
        notes={items}
        totalCount={totalCount}
        page={pageNumber}
        pageSize={pageSize}
        onPageChange={setPage}
        onNew={() => setIsModalOpen(true)}
      />
      <DeliveryNoteFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        companyId={companyId}
      />
    </div>
  )
}
