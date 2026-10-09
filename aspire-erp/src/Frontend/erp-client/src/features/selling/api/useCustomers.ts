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
  customerType?: string
  customerGroup?: string
  territory?: string
  billingAddress?: string
  phone?: string
  email?: string
  contactPerson?: string
  website?: string
  paymentTerms?: string
  customerDetails?: string
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
    customerType: payload.customerType || 'Company',
    customerGroup: payload.customerGroup ?? '',
    territory: payload.territory ?? '',
    billingAddress: payload.billingAddress ?? '',
    phone: payload.phone ?? '',
    email: payload.email ?? '',
    contactPerson: payload.contactPerson ?? '',
    website: payload.website ?? '',
    paymentTerms: payload.paymentTerms ?? '',
    customerDetails: payload.customerDetails ?? '',
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
    customerType: payload.customerType ?? null,
    customerGroup: payload.customerGroup ?? null,
    territory: payload.territory ?? null,
    billingAddress: payload.billingAddress ?? null,
    phone: payload.phone ?? null,
    email: payload.email ?? null,
    contactPerson: payload.contactPerson ?? null,
    website: payload.website ?? null,
    paymentTerms: payload.paymentTerms ?? null,
    customerDetails: payload.customerDetails ?? null,
  })
  return response.data
}
