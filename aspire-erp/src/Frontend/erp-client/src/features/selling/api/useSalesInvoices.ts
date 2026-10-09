import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export interface SalesInvoiceItem {
  id: string
  itemId: string
  salesOrderItemId?: string | null
  quantity: number
  rate: number
  amount: number
}

export interface SalesInvoice {
  id: string
  companyId: string
  invoiceNumber: string
  customerId: string
  customerName?: string | null
  postingDate: string
  dueDate: string
  status: string
  isPOS: boolean
  updateStock: boolean
  sourceWarehouseId?: string | null
  netTotal: number
  taxTotal: number
  grandTotal: number
  outstandingAmount: number
  paidAmount: number
  rowVersion?: string
  createdAt: string
  items: SalesInvoiceItem[]
}

export function useSalesInvoices(companyId: string, page = 1, pageSize = 50) {
  return useApiList<SalesInvoice>('/v1/sales-invoices', { companyId, page, pageSize }, Boolean(companyId))
}

export async function createSalesInvoice(payload: {
  companyId: string
  customerId: string
  postingDate: string
  items: { itemId: string; quantity: number; rate: number }[]
}): Promise<SalesInvoice> {
  const response = await apiClient.post<SalesInvoice>('/v1/sales-invoices', {
    companyId: payload.companyId,
    customerId: payload.customerId,
    postingDate: payload.postingDate,
    items: payload.items,
  })
  return response.data
}

export async function submitSalesInvoice(id: string, companyId: string): Promise<SalesInvoice> {
  const response = await apiClient.post<SalesInvoice>(`/v1/sales-invoices/${id}/submit`, null, {
    params: { companyId },
  })
  return response.data
}
