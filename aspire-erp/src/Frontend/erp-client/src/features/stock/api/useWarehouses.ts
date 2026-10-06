import { useState, useEffect, useCallback } from 'react'
import { apiClient, ApiError } from '../../../api/client'

export interface Warehouse {
  id: string
  companyId: string
  code: string
  name: string
  accountId: string
  parentWarehouseId?: string
  isGroup: boolean
  isActive: boolean
  rowVersion: string
}

export function useWarehouses(companyId: string) {
  const [data, setData] = useState<Warehouse[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  const fetchWarehouses = useCallback(async () => {
    if (!companyId) return
    setLoading(true)
    setError(null)
    try {
      const response = await apiClient.get<Warehouse[]>('/v1/warehouses/tree', {
        params: { companyId },
      })
      setData(response.data)
    } catch (err) {
      if (err instanceof ApiError) setError(err)
    } finally {
      setLoading(false)
    }
  }, [companyId])

  useEffect(() => {
    fetchWarehouses()
  }, [fetchWarehouses])

  const createWarehouse = async (warehouseData: Partial<Warehouse>) => {
    const response = await apiClient.post<Warehouse>('/v1/warehouses', warehouseData)
    await fetchWarehouses()
    return response.data
  }

  const updateWarehouse = async (id: string, warehouseData: Partial<Warehouse>) => {
    const response = await apiClient.put<Warehouse>(`/v1/warehouses/${id}`, warehouseData)
    await fetchWarehouses()
    return response.data
  }

  return {
    data,
    loading,
    error,
    createWarehouse,
    updateWarehouse,
    refetch: fetchWarehouses,
  }
}
