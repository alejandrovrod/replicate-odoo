import { useState, useEffect, useCallback } from 'react'
import { apiClient } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'

export interface AssetCategory {
  id: string
  companyId: string
  categoryName: string
  fixedAssetAccountId: string
  accumulatedDepreciationAccountId: string
  depreciationExpenseAccountId: string
  cwipAccountId?: string
  gainOnDisposalAccountId?: string
  lossOnDisposalAccountId?: string
  isNonDepreciable: boolean
  isActive: boolean
  createdAt: string
  rowVersion?: string
}

interface PaginatedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export function useAssetCategories(page: number = 1, pageSize: number = 50) {
  const companyId = useTenantStore((state) => state.companyId)
  const [items, setItems] = useState<AssetCategory[]>([])
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle')
  const [error, setError] = useState<Error | null>(null)
  
  const [totalCount, setTotalCount] = useState(0)
  const [pageNumber, setPageNumber] = useState(1)

  const fetchCategories = useCallback(async () => {
    if (!companyId) return
    setStatus('loading')
    try {
      const response = await apiClient.get<PaginatedResponse<AssetCategory>>(`/v1/AssetCategories?companyId=${companyId}&page=${page}&pageSize=${pageSize}`)
      setItems(response.data.items || [])
      setTotalCount(response.data.totalCount || 0)
      setPageNumber(response.data.page || 1)
      setStatus('success')
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Failed to load asset categories'))
      setStatus('error')
    }
  }, [companyId, page, pageSize])

  useEffect(() => {
    fetchCategories()
  }, [fetchCategories])

  return { items, status, error, reload: fetchCategories, totalCount, pageNumber }
}

export async function createAssetCategory(data: Partial<AssetCategory>): Promise<AssetCategory> {
  const response = await apiClient.post<AssetCategory>('/v1/AssetCategories', {
    companyId: data.companyId,
    categoryName: data.categoryName,
    fixedAssetAccountId: data.fixedAssetAccountId,
    accumulatedDepreciationAccountId: data.accumulatedDepreciationAccountId,
    depreciationExpenseAccountId: data.depreciationExpenseAccountId,
    cwipAccountId: data.cwipAccountId ?? null,
    gainOnDisposalAccountId: data.gainOnDisposalAccountId ?? null,
    lossOnDisposalAccountId: data.lossOnDisposalAccountId ?? null,
    isNonDepreciable: data.isNonDepreciable ?? false,
  })
  return response.data
}

/**
 * PUT body (mirrors `UpdateAssetCategoryCommand`): always carries the original
 * `rowVersion` so a concurrent change resolves to 409 instead of silently winning.
 */
export async function updateAssetCategory(id: string, data: Partial<AssetCategory>): Promise<AssetCategory> {
  const response = await apiClient.put<AssetCategory>(`/v1/AssetCategories/${id}`, {
    id,
    companyId: data.companyId,
    categoryName: data.categoryName,
    fixedAssetAccountId: data.fixedAssetAccountId,
    accumulatedDepreciationAccountId: data.accumulatedDepreciationAccountId,
    depreciationExpenseAccountId: data.depreciationExpenseAccountId,
    cwipAccountId: data.cwipAccountId ?? null,
    gainOnDisposalAccountId: data.gainOnDisposalAccountId ?? null,
    lossOnDisposalAccountId: data.lossOnDisposalAccountId ?? null,
    isNonDepreciable: data.isNonDepreciable ?? false,
    isActive: data.isActive ?? true,
    rowVersion: data.rowVersion,
  })
  return response.data
}
