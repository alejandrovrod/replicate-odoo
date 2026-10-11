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

/** Mirrors SalesInvoiceTaxDto (module 17): amounts always arrive server-computed. */
export interface SalesInvoiceTax {
  id: string
  accountId: string
  rate: number
  taxAmount: number
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
  /** Module 17: global discount + tax breakdown (absent on pre-module invoices). */
  discountPercentage?: number
  discountAmount?: number
  taxes?: SalesInvoiceTax[]
  /** Module 18: credit-note mode + provenance. */
  isReturn?: boolean
  returnAgainstId?: string | null
}

export function useSalesInvoices(companyId: string, page = 1, pageSize = 50) {
  return useApiList<SalesInvoice>('/v1/sales-invoices', { companyId, page, pageSize }, Boolean(companyId))
}

export interface CreateSalesInvoicePayload {
  companyId: string
  customerId: string
  postingDate: string
  items: { itemId: string; quantity: number; rate: number }[]
  /** Module 17: percentage wins when no explicit amount travels (both must agree). */
  discountPercentage?: number
  discountAmount?: number
  /** Module 17: only account + rate travel - amounts are recomputed server-side. */
  taxes?: { accountId: string; rate: number }[]
  /** Module 18: credit-note mode; lines must carry negative quantities. */
  isReturn?: boolean
  returnAgainstId?: string | null
}

export async function createSalesInvoice(payload: CreateSalesInvoicePayload): Promise<SalesInvoice> {
  const response = await apiClient.post<SalesInvoice>('/v1/sales-invoices', {
    companyId: payload.companyId,
    customerId: payload.customerId,
    postingDate: payload.postingDate,
    items: payload.items,
    discountPercentage: payload.discountPercentage ?? 0,
    discountAmount: payload.discountAmount ?? 0,
    taxes: payload.taxes ?? [],
    isReturn: payload.isReturn ?? false,
    returnAgainstId: payload.returnAgainstId ?? null,
  })
  return response.data
}

export async function submitSalesInvoice(id: string, companyId: string): Promise<SalesInvoice> {
  const response = await apiClient.post<SalesInvoice>(`/v1/sales-invoices/${id}/submit`, null, {
    params: { companyId },
  })
  return response.data
}
