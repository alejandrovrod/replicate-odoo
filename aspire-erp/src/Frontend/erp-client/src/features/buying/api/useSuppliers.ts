import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

/**
 * Mirrors `SupplierDto` (GET / POST / PUT /api/v1/suppliers) as serialized by
 * System.Text.Json: camelCase properties, `rowVersion` as a base64 string.
 */
export interface Supplier {
  id: string
  code: string
  name: string
  taxId?: string
  defaultPayableAccountId?: string | null
  currencyId?: string | null
  paymentTermsDays?: number
  outstandingAmount?: number
  isActive: boolean
  createdAt?: string
  /** Base64 rowversion token from GET; echoed back on PUT for optimistic concurrency. */
  rowVersion?: string
}

export function useSuppliers(page = 1, pageSize = 50) {
  // Suppliers are unique per tenant, so we don't necessarily pass companyId unless it's for filtering
  return useApiList<Supplier>('/v1/suppliers', { page, pageSize }, true)
}

/** POST body (mirrors `CreateSupplierCommand`). */
export async function createSupplier(payload: Partial<Supplier>): Promise<Supplier> {
  const response = await apiClient.post<Supplier>('/v1/suppliers', {
    code: payload.code,
    name: payload.name,
    taxId: payload.taxId ?? '',
    defaultPayableAccountId: payload.defaultPayableAccountId ?? null,
    currencyId: payload.currencyId ?? null,
    paymentTermsDays: payload.paymentTermsDays ?? 30,
    isActive: payload.isActive ?? true,
  })
  return response.data
}

/**
 * PUT body (mirrors `UpdateSupplierCommand`): always carries the original
 * `rowVersion` so a concurrent change resolves to 409 instead of silently winning.
 */
export async function updateSupplier(id: string, payload: Partial<Supplier>): Promise<Supplier> {
  const response = await apiClient.put<Supplier>(`/v1/suppliers/${id}`, {
    id,
    code: payload.code,
    name: payload.name,
    taxId: payload.taxId ?? '',
    paymentTermsDays: payload.paymentTermsDays ?? 30,
    payableAccountId: payload.defaultPayableAccountId ?? null,
    isActive: payload.isActive ?? true,
    rowVersion: payload.rowVersion,
  })
  return response.data
}
