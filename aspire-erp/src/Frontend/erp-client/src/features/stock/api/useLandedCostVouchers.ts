import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export type LandedCostDistributeBy = 'Amount' | 'Quantity'
export type LandedCostStatus = 'Draft' | 'Submitted' | 'Cancelled'

/** One charge as stored (mirrors `LandedCostChargeDto`). */
export interface LandedCostCharge {
  id: string
  description: string
  amount: number
  expenseAccountId: string
  expenseAccountCode: string
  expenseAccountName: string
}

/** One apportioned line as stored (mirrors `LandedCostApportionmentDto`). */
export interface LandedCostItem {
  purchaseReceiptId: string
  purchaseReceiptNo: string
  purchaseReceiptLineId: string
  itemId: string
  itemCode: string
  itemName: string
  qty: number
  originalRate: number
  applicableCharges: number
  newRate: number
}

/** Voucher payload (mirrors `LandedCostVoucherDto`). */
export interface LandedCostVoucher {
  id: string
  companyId: string
  voucherNo: string
  postingDate: string
  status: LandedCostStatus
  distributeBy: LandedCostDistributeBy
  totalTaxesAndCharges: number
  rowVersion?: string
  createdAt: string
  receiptIds: string[]
  charges: LandedCostCharge[]
  items: LandedCostItem[]
}

/** Submitted purchase receipt row (subset of the receipts list needed by the selector). */
export interface PurchaseReceiptOption {
  id: string
  voucherNo: string
  postingDate: string
  status: string
  warehouseId: string
  lines: {
    id: string
    itemId: string
    itemCode: string
    itemName: string
    qty: number
    rate: number
  }[]
}

export function useLandedCostVouchers(companyId: string, page = 1, pageSize = 50) {
  return useApiList<LandedCostVoucher>(
    '/v1/landed-cost-vouchers',
    { companyId, page, pageSize },
    Boolean(companyId),
  )
}

export async function getLandedCostVoucher(id: string, companyId: string): Promise<LandedCostVoucher> {
  const response = await apiClient.get<LandedCostVoucher>(`/v1/landed-cost-vouchers/${id}`, {
    params: { companyId },
  })
  return response.data
}

export interface CreateLandedCostPayload {
  companyId: string
  postingDate: string
  distributeBy: LandedCostDistributeBy
  receiptIds: string[]
  charges: { description: string; amount: number; expenseAccountId: string }[]
}

export async function createLandedCostVoucher(payload: CreateLandedCostPayload): Promise<LandedCostVoucher> {
  const response = await apiClient.post<LandedCostVoucher>('/v1/landed-cost-vouchers', {
    companyId: payload.companyId,
    postingDate: payload.postingDate,
    distributeBy: payload.distributeBy,
    receiptIds: payload.receiptIds,
    charges: payload.charges,
  })
  return response.data
}

export async function submitLandedCostVoucher(
  id: string,
  companyId: string,
  rowVersion?: string,
): Promise<LandedCostVoucher> {
  const response = await apiClient.post<LandedCostVoucher>(
    `/v1/landed-cost-vouchers/${id}/submit`,
    { rowVersion: rowVersion ?? null },
    { params: { companyId } },
  )
  return response.data
}

export async function cancelLandedCostVoucher(
  id: string,
  companyId: string,
  rowVersion?: string,
): Promise<LandedCostVoucher> {
  const response = await apiClient.post<LandedCostVoucher>(
    `/v1/landed-cost-vouchers/${id}/cancel`,
    { rowVersion: rowVersion ?? null },
    { params: { companyId } },
  )
  return response.data
}

export function usePurchaseReceiptOptions(companyId: string, page = 1, pageSize = 100) {
  return useApiList<PurchaseReceiptOption>(
    '/v1/purchasereceipts',
    { companyId, page, pageSize },
    Boolean(companyId),
  )
}
