import { apiClient } from '../../../api/client'
import { useApiList, useApiTreeList } from '../../../lib/useApiList'

export interface SalesInvoiceItem {
  id: string
  itemId: string
  salesOrderItemId?: string | null
  quantity: number
  rate: number
  amount: number
}

/** Mirrors SalesInvoiceTaxDto (tax-engine Fase 2): amounts always arrive server-computed. */
export interface SalesInvoiceTax {
  id: string
  accountId: string
  rate: number
  taxAmount: number
  chargeType?: 'Add' | 'Deduct'
  calculationBasis?: 'OnNetTotal' | 'OnPreviousRow' | 'FixedAmount'
  baseAmount?: number
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
  /** Advance allocation: total crossed at creation, settled at submit. */
  totalAdvance: number
  /** Per-payment cross slices (populated on detail reads). */
  advances?: SalesInvoiceAdvance[]
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

/** One advance-cross slice (mirrors PaymentAllocationDto): which payment funds how much. */
export interface SalesInvoiceAdvance {
  id: string
  paymentEntryId?: string | null
  allocatedAmount: number
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
  /** Module 17 + tax-engine Fase 2: computation shape travels, amounts recomputed server-side. */
  taxes?: {
    accountId: string
    rate: number
    chargeType?: 'Add' | 'Deduct'
    calculationBasis?: 'OnNetTotal' | 'OnPreviousRow' | 'FixedAmount'
    fixedAmount?: number
  }[]
  /** Module 18: credit-note mode; lines must carry negative quantities. */
  isReturn?: boolean
  returnAgainstId?: string | null
  /** Immediate stock update: delivery from the source warehouse. */
  updateStock?: boolean
  sourceWarehouseId?: string | null
  /** Advance allocation: slices reserved at creation, settled at submit. */
  advances?: { paymentEntryId: string; allocatedAmount: number }[]
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
    updateStock: payload.updateStock ?? false,
    sourceWarehouseId: payload.sourceWarehouseId ?? null,
    advances: payload.advances ?? [],
  })
  return response.data
}

export async function submitSalesInvoice(id: string, companyId: string): Promise<SalesInvoice> {
  const response = await apiClient.post<SalesInvoice>(`/v1/sales-invoices/${id}/submit`, null, {
    params: { companyId },
  })
  return response.data
}

/** One advance funding source (mirrors EligibleAdvanceDto): submitted receipt + remainder. */
export interface EligibleAdvance {
  paymentEntryId: string
  voucherNo: string
  paymentDate: string
  paidAmount: number
  unallocatedAmount: number
}

/**
 * GET /v1/customers/{customerId}/eligible-advances - submitted customer receipts with a
 * remaining unallocated balance, oldest first (T4.1: the advance grid's source).
 */
export function useEligibleAdvances(companyId: string, customerId: string) {
  return useApiTreeList<EligibleAdvance>(
    `/v1/customers/${customerId}/eligible-advances`,
    { companyId },
    Boolean(companyId) && Boolean(customerId),
  )
}

export async function getEligibleAdvances(
  customerId: string,
  companyId: string,
): Promise<EligibleAdvance[]> {
  const response = await apiClient.get<EligibleAdvance[]>(
    `/v1/customers/${customerId}/eligible-advances`,
    { params: { companyId } },
  )
  return response.data
}
