import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'
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

/** One General Ledger line the voucher posted (backend `GLEntryDto`). */
export interface GlEntry {
  id: number
  accountId: string
  accountCode: string
  accountName: string
  postingDate: string
  debit: number
  credit: number
  voucherType: string
  voucherNo: string
  remarks?: string | null
}

/** Detail payload of `GET /v1/PaymentEntries/{id}` (header + allocations + posted GL). */
export interface PaymentEntryDetail {
  payment: PaymentEntry
  allocations: PaymentAllocation[]
  glEntries: GlEntry[]
}

/** Optional server-side filters of the payment list (mapped 1:1 onto the query string). */
export interface PaymentEntryFilters {
  status?: string
  paymentType?: string
}

export function usePaymentEntries(
  companyId: string,
  page = 1,
  pageSize = 50,
  filters: PaymentEntryFilters = {},
) {
  return useApiList<PaymentEntry>(
    '/v1/PaymentEntries',
    {
      companyId,
      page,
      pageSize,
      status: filters.status || undefined,
      paymentType: filters.paymentType || undefined,
    },
    Boolean(companyId),
  )
}

export interface PaymentDetailQuery {
  detail: PaymentEntryDetail | null
  status: 'idle' | 'loading' | 'success' | 'error'
  error: ApiError | null
  reload: () => void
}

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

/**
 * Single-object GET loader for the detail dialog. Unlike `useApiList` this is not paged, so it
 * stays a small purpose-built hook; the `attempt` counter backs the same `reload()` contract.
 */
export function usePaymentDetail(id: string | null, companyId: string): PaymentDetailQuery {
  const [attempt, setAttempt] = useState(0)
  const [detail, setDetail] = useState<PaymentEntryDetail | null>(null)
  const [status, setStatus] = useState<PaymentDetailQuery['status']>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    if (!id || !companyId) {
      setDetail(null)
      setStatus('idle')
      setError(null)
      return undefined
    }
    let cancelled = false
    setStatus('loading')
    setError(null)
    getPaymentDetail(id, companyId).then(
      (result) => {
        if (!cancelled) {
          setDetail(result)
          setStatus('success')
        }
      },
      (cause: unknown) => {
        if (!cancelled) {
          setDetail(null)
          setError(toApiError(cause))
          setStatus('error')
        }
      },
    )
    return () => {
      cancelled = true
    }
  }, [id, companyId, attempt])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  return { detail, status, error, reload }
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

/** Header + allocations + posted GL lines of one voucher (read-only detail view). */
export async function getPaymentDetail(id: string, companyId: string): Promise<PaymentEntryDetail> {
  const response = await apiClient.get<PaymentEntryDetail>(`/v1/PaymentEntries/${id}`, {
    params: { companyId },
  })
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
