import { apiClient } from '../../api/client'
import { useApiList, useApiTreeList, type QueryStatus } from '../../lib/useApiList'
import type { FlatWarehouse, Item, StockEntry, StockEntryPosting, StockEntryType, WarehouseNode } from './types'

export type { QueryStatus }

/** GET /api/v1/items?companyId=&page=&pageSize= - SKUs with their live per-warehouse stock (Task 3.4). */
export function useItems(companyId: string, page = 1, pageSize = 50) {
  return useApiList<Item>('/v1/items', { companyId, page, pageSize }, Boolean(companyId))
}

/** GET /api/v1/warehouses/tree?companyId= - the nested warehouse hierarchy (tree: unpaged by design). */
export function useWarehouses(companyId: string) {
  return useApiTreeList<WarehouseNode>('/v1/warehouses/tree', { companyId }, Boolean(companyId))
}

/** GET /api/v1/warehouses - the flat warehouse list paginated. */
export function useFlatWarehouses(companyId: string, page = 1, pageSize = 6, leavesOnly = true) {
  return useApiList<FlatWarehouse>('/v1/warehouses', { companyId, page, pageSize, leavesOnly }, Boolean(companyId))
}

/** GET /api/v1/stockentries?companyId=&page=&pageSize= - the most recent posted vouchers. */
export function useStockEntries(companyId: string, page = 1, pageSize = 20) {
  return useApiList<StockEntry>('/v1/stockentries', { companyId, page, pageSize }, Boolean(companyId))
}

/** Body of POST /api/v1/stockentries (mirrors `CreateStockEntryCommand`). */
export interface CreateStockEntryPayload {
  companyId: string
  entryType: StockEntryType
  warehouseId: string
  targetWarehouseId?: string
  postingDate: string
  lines: { itemId: string; qty: number; rate?: number }[]
}

/**
 * POST /api/v1/stockentries - creates AND posts the voucher atomically.
 *
 * `Idempotency-Key` is mandatory (Constitution VI.4): replaying the same key with the same body
 * returns the original 201 instead of posting twice, which is what makes a retry of the same
 * modal submission safe. The server releases the reservation when the request fails, so the
 * form can be corrected and re-posted under the same key.
 */
export async function postStockEntry(
  payload: CreateStockEntryPayload,
  idempotencyKey: string,
): Promise<StockEntryPosting> {
  const response = await apiClient.post<StockEntryPosting>('/v1/stockentries', payload, {
    headers: { 'Idempotency-Key': idempotencyKey },
  })
  return response.data
}
