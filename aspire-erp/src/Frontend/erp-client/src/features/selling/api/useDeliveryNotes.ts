import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export interface DeliveryNoteLine {
  id: string
  salesOrderItemId: string
  itemId: string
  itemCode: string
  itemName: string
  qty: number
  /** Batch-and-serial Fase 3: shipped batch (null for untracked lines). */
  batchId?: string | null
}

export interface DeliveryNote {
  id: string
  companyId: string
  salesOrderId: string
  warehouseId: string
  postingDate: string
  voucherNo: string
  createdAt: string
  lines: DeliveryNoteLine[]
}

export function useDeliveryNotes(companyId: string, page = 1, pageSize = 50) {
  return useApiList<DeliveryNote>('/v1/delivery-notes', { companyId, page, pageSize }, Boolean(companyId))
}

export async function postDeliveryNote(payload: {
  companyId: string
  salesOrderId: string
  warehouseId: string
  postingDate: string
  lines: {
    salesOrderItemId: string
    itemId: string
    qty: number
    /** Batch-and-serial Fase 3: shipped batch + exact serials when tracked. */
    batchId?: string | null
    serialNumbers?: string[]
  }[]
}): Promise<unknown> {
  const response = await apiClient.post('/v1/delivery-notes', {
    companyId: payload.companyId,
    salesOrderId: payload.salesOrderId,
    warehouseId: payload.warehouseId,
    postingDate: payload.postingDate,
    lines: payload.lines,
  })
  return response.data
}
