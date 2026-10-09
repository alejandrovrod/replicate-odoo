import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export interface SalesOrderLine {
  id: string
  itemId: string
  itemCode: string
  itemName: string
  quantity: number
  deliveredQuantity: number
  billedQuantity: number
  rate: number
  amount: number
  deliveredPercentage: number
  billedPercentage: number
}

export interface SalesOrder {
  id: string
  companyId: string
  customerId: string
  customerCode: string
  customerName: string
  status: string
  transactionDate: string
  deliveryDate: string
  orderNumber: string
  netTotal: number
  taxTotal: number
  grandTotal: number
  deliveredPercentage: number
  billedPercentage: number
  createdAt: string
  lines: SalesOrderLine[]
}

export interface CreateSalesOrderLineInput {
  itemId: string
  quantity: number
  rate: number
}

export function useSalesOrders(companyId: string, page = 1, pageSize = 50) {
  return useApiList<SalesOrder>('/v1/sales-orders', { companyId, page, pageSize }, Boolean(companyId))
}

export async function createSalesOrder(payload: {
  companyId: string
  customerId: string
  transactionDate: string
  deliveryDate: string
  lines: CreateSalesOrderLineInput[]
}): Promise<SalesOrder> {
  const response = await apiClient.post<SalesOrder>('/v1/sales-orders', {
    companyId: payload.companyId,
    customerId: payload.customerId,
    transactionDate: payload.transactionDate,
    deliveryDate: payload.deliveryDate,
    lines: payload.lines.map((l) => ({
      itemId: l.itemId,
      quantity: l.quantity,
      rate: l.rate,
    })),
  })
  return response.data
}

export async function submitSalesOrder(id: string, companyId: string): Promise<SalesOrder> {
  const response = await apiClient.post<SalesOrder>(`/v1/sales-orders/${id}/submit`, null, {
    params: { companyId },
  })
  return response.data
}
