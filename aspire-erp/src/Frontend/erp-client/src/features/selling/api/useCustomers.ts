import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export interface Customer {
  id: string
  companyId: string
  customerCode: string
  name: string
  taxId?: string
  creditLimit?: number
  paymentTermId?: string
  receivableAccountId?: string
  isActive: boolean
  rowVersion: string
}

export function useCustomers(companyId: string, page = 1, pageSize = 50) {
  return useApiList<Customer>('/v1/customers', { companyId, page, pageSize }, Boolean(companyId))
}

export async function createCustomer(payload: Partial<Customer>): Promise<Customer> {
  const response = await apiClient.post<Customer>('/v1/customers', payload)
  return response.data
}

export async function updateCustomer(id: string, payload: Partial<Customer>): Promise<Customer> {
  const response = await apiClient.put<Customer>(`/v1/customers/${id}`, payload)
  return response.data
}
