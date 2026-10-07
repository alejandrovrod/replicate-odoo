import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export interface Supplier {
  id: string
  supplierCode: string
  name: string
  taxId?: string
  paymentTermId?: string
  payableAccountId?: string
  isActive: boolean
  rowVersion: string
}

export function useSuppliers(page = 1, pageSize = 50) {
  // Suppliers are unique per tenant, so we don't necessarily pass companyId unless it's for filtering
  return useApiList<Supplier>('/v1/suppliers', { page, pageSize })
}

export async function createSupplier(payload: Partial<Supplier>): Promise<Supplier> {
  const response = await apiClient.post<Supplier>('/v1/suppliers', payload)
  return response.data
}

export async function updateSupplier(id: string, payload: Partial<Supplier>): Promise<Supplier> {
  const response = await apiClient.put<Supplier>(`/v1/suppliers/${id}`, payload)
  return response.data
}
