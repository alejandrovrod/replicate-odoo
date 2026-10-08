import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

/**
 * Mirrors `CustomerDto` (GET / POST / PUT /api/v1/customers) as serialized by
 * System.Text.Json: camelCase properties, `rowVersion` as a base64 string.
 */
export interface Customer {
  id: string
  companyId: string
  code: string
  name: string
  taxId?: string
  defaultReceivableAccountId?: string | null
  creditLimit?: number
  bypassCreditLimitCheck?: boolean
  currencyId?: string | null
  paymentTermsDays?: number
  outstandingAmount?: number
  isActive: boolean
  /** Base64 rowversion token from GET; echoed back on PUT for optimistic concurrency. */
  rowVersion?: string
}

export function useCustomers(companyId: string, page = 1, pageSize = 50) {
  return useApiList<Customer>('/v1/customers', { companyId, page, pageSize }, Boolean(companyId))
}

/** POST body (mirrors `CreateCustomerCommand`). */
export async function createCustomer(payload: Partial<Customer>): Promise<Customer> {
  const response = await apiClient.post<Customer>('/v1/customers', {
    companyId: payload.companyId,
    customerCode: payload.code,
    customerName: payload.name,
    taxId: payload.taxId ?? '',
    creditLimit: payload.creditLimit ?? 0,
    bypassCreditLimitCheck: payload.bypassCreditLimitCheck ?? false,
    currencyId: payload.currencyId ?? null,
    paymentTermsDays: payload.paymentTermsDays ?? 30,
    defaultReceivableAccountId: payload.defaultReceivableAccountId ?? null,
    isActive: payload.isActive ?? true,
  })
  return response.data
}

/**
 * PUT body (mirrors `UpdateCustomerCommand`): always carries the original
 * `rowVersion` so a concurrent change resolves to 409 instead of silently winning.
 */
export async function updateCustomer(id: string, payload: Partial<Customer>): Promise<Customer> {
  const response = await apiClient.put<Customer>(`/v1/customers/${id}`, {
    id,
    companyId: payload.companyId,
    code: payload.code,
    name: payload.name,
    taxId: payload.taxId ?? '',
    creditLimit: payload.creditLimit ?? 0,
    paymentTermsDays: payload.paymentTermsDays ?? 30,
    receivableAccountId: payload.defaultReceivableAccountId ?? null,
    isActive: payload.isActive ?? true,
    rowVersion: payload.rowVersion,
  })
  return response.data
}
