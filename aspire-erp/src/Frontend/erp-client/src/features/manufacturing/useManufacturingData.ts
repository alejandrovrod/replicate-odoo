import { apiClient } from '../../api/client'
import { useApiList, type QueryStatus } from '../../lib/useApiList'
import type { Bom, ManufacturingPosting, WorkOrder } from './types'

export type { QueryStatus }

/** GET /api/v1/workorders?companyId=&page=&pageSize= - headers for the execution board. */
export function useWorkOrders(companyId: string, page = 1, pageSize = 50) {
  return useApiList<WorkOrder>('/v1/workorders', { companyId, page, pageSize }, Boolean(companyId))
}

/** GET /api/v1/boms?companyId=&page=&pageSize= - recipes for the BOM tree editor. */
export function useBoms(companyId: string, page = 1, pageSize = 50) {
  return useApiList<Bom>('/v1/boms', { companyId, page, pageSize }, Boolean(companyId))
}

/**
 * POST /api/v1/workorders/{id}/submit - Draft -> Submitted (no ledger impact, but the
 * explicit key keeps the call contract uniform with the posting mutations).
 */
export async function postSubmitWorkOrder(orderId: string, companyId: string): Promise<WorkOrder> {
  const response = await apiClient.post<WorkOrder>(
    `/v1/workorders/${orderId}/submit`,
    null,
    { params: { companyId }, headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  return response.data
}

/**
 * POST /api/v1/workorders/{id}/transfer-to-wip - Submitted -> InProcess (MF-02 voucher).
 * `Idempotency-Key` is mandatory (Constitution VI.4): retrying the same key replays the
 * original posting instead of moving stock twice.
 */
export async function postTransferToWip(orderId: string, companyId: string): Promise<ManufacturingPosting> {
  const response = await apiClient.post<ManufacturingPosting>(
    `/v1/workorders/${orderId}/transfer-to-wip`,
    null,
    { params: { companyId }, headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  return response.data
}

/**
 * POST /api/v1/workorders/{id}/complete - InProcess -> Completed (MF-03 voucher).
 * Same idempotency contract as the transfer (spec MF-04 replay safety).
 */
export async function postCompleteManufacture(
  orderId: string,
  companyId: string,
  producedQuantity: number,
): Promise<ManufacturingPosting> {
  const response = await apiClient.post<ManufacturingPosting>(
    `/v1/workorders/${orderId}/complete`,
    null,
    {
      params: { companyId, producedQuantity },
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    },
  )
  return response.data
}

/**
 * POST /api/v1/workorders/{id}/cancel - Submitted/InProcess -> Cancelled (MF-05), with a
 * compensating WIP -> Stores voucher when materials were issued.
 */
export async function postCancelWorkOrder(orderId: string, companyId: string): Promise<WorkOrder> {
  const response = await apiClient.post<WorkOrder>(
    `/v1/workorders/${orderId}/cancel`,
    null,
    { params: { companyId }, headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  return response.data
}
