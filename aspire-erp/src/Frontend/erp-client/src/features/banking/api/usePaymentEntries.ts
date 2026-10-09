import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export interface PaymentAllocation {
  id: string
  salesInvoiceId?: string | null
  purchaseInvoiceId?: string | null
  allocatedAmount: number
  referenceDocumentType?: string
  referenceDocumentId?: string | null
  totalAmount?: number
  outstandingAmount?: number
  exchangeRate?: number
}

export interface PaymentEntry {
  id: string
  companyId: string
  voucherNo: string
  paymentType: string
  partyType: string
  partyId: string
  bankAccountId: string
  paymentDate: string
  paidAmount: number
  unallocatedAmount: number
  referenceNumber?: string | null
  documentStatus: string
  status: string
  clearanceDate?: string | null
  rowVersion?: string
  partyName?: string
  modeOfPayment?: string
  paidFromAccountId?: string | null
  paidToAccountId?: string | null
  totalAllocatedAmount?: number
  differenceAmount?: number
  referenceDate?: string | null
  costCenterId?: string | null
  projectId?: string | null
  remarks?: string
}

export interface OutstandingInvoice {
  id: string
  number: string
  postingDate: string
  dueDate: string
  grandTotal: number
  outstandingAmount: number
  status: string
}

export function usePaymentEntries(companyId: string, page = 1, pageSize = 50) {
  return useApiList<PaymentEntry>('/v1/PaymentEntries', { companyId, page, pageSize }, Boolean(companyId))
}

export async function createPaymentEntry(payload: {
  companyId: string
  paymentType: string
  partyType: string
  partyId: string
  bankAccountId: string
  paymentDate: string
  paidAmount: number
  transactionCurrencyId?: string | null
  referenceNumber?: string | null
  allocations: { salesInvoiceId?: string | null; purchaseInvoiceId?: string | null; allocatedAmount: number }[]
  partyName?: string
  modeOfPayment?: string
  referenceDate?: string | null
  remarks?: string
}): Promise<PaymentEntry> {
  const response = await apiClient.post<PaymentEntry>('/v1/PaymentEntries', {
    companyId: payload.companyId,
    paymentType: payload.paymentType,
    partyType: payload.partyType,
    partyId: payload.partyId,
    bankAccountId: payload.bankAccountId,
    paymentDate: payload.paymentDate,
    paidAmount: payload.paidAmount,
    transactionCurrencyId: payload.transactionCurrencyId ?? null,
    referenceNumber: payload.referenceNumber ?? null,
    allocations: payload.allocations,
    partyName: payload.partyName ?? '',
    modeOfPayment: payload.modeOfPayment ?? '',
    referenceDate: payload.referenceDate ?? null,
    remarks: payload.remarks ?? '',
  })
  return response.data
}

export async function submitPaymentEntry(id: string, companyId: string, rowVersion?: string): Promise<PaymentEntry> {
  const response = await apiClient.post<PaymentEntry>(
    `/v1/PaymentEntries/${id}/submit`,
    { rowVersion: rowVersion ?? null },
    { params: { companyId } },
  )
  return response.data
}

export async function cancelPaymentEntry(id: string, companyId: string, rowVersion?: string): Promise<PaymentEntry> {
  const response = await apiClient.post<PaymentEntry>(
    `/v1/PaymentEntries/${id}/cancel`,
    { rowVersion: rowVersion ?? null },
    { params: { companyId } },
  )
  return response.data
}

export async function getOutstandingInvoices(
  partyKind: 'customers' | 'suppliers',
  partyId: string,
  companyId: string,
): Promise<OutstandingInvoice[]> {
  const response = await apiClient.get<OutstandingInvoice[]>(`/v1/${partyKind}/${partyId}/outstanding-invoices`, {
    params: { companyId },
  })
  return response.data
}
