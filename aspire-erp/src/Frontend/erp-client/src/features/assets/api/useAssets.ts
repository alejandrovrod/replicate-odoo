import { useState, useEffect, useCallback } from 'react'
import { apiClient } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'

export type AssetStatus = 'Draft' | 'Capitalized' | 'Sold' | 'Scrapped'
export type DepreciationMethod = 'StraightLine' | 'DecliningBalance'

export interface Asset {
  id: string
  companyId: string
  assetCode: string
  assetName: string
  itemId: string
  assetCategoryId: string
  purchaseDate: string
  availableForUseDate: string
  grossPurchaseAmount: number
  salvageValue: number
  accumulatedDepreciation: number
  netBookValue: number
  depreciationMethod: DepreciationMethod
  totalNumberOfDepreciations: number
  frequencyInMonths: number
  status: AssetStatus
  disposalDate?: string | null
  createdAt: string
}

export interface AssetScheduleLine {
  id: string
  scheduleDate: string
  depreciationAmount: number
  accumulatedDepreciationAfter: number
  status: 'Scheduled' | 'Booked' | 'Cancelled'
}

export interface AssetDetail {
  asset: Asset
  schedule: AssetScheduleLine[]
}

interface PaginatedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export function useAssets(page: number = 1, pageSize: number = 50) {
  const companyId = useTenantStore((state) => state.companyId)
  const [items, setItems] = useState<Asset[]>([])
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle')
  const [error, setError] = useState<Error | null>(null)
  
  const [totalCount, setTotalCount] = useState(0)
  const [pageNumber, setPageNumber] = useState(1)

  const fetchAssets = useCallback(async () => {
    if (!companyId) return
    setStatus('loading')
    try {
      const data = await apiClient.get<PaginatedResponse<Asset>>(`/v1/Assets?companyId=${companyId}&page=${page}&pageSize=${pageSize}`)
      setItems(data.items || [])
      setTotalCount(data.totalCount || 0)
      setPageNumber(data.page || 1)
      setStatus('success')
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Failed to load assets'))
      setStatus('error')
    }
  }, [companyId, page, pageSize])

  useEffect(() => {
    fetchAssets()
  }, [fetchAssets])

  return { items, status, error, reload: fetchAssets, totalCount, pageNumber }
}

export async function getAssetDetail(id: string, companyId: string): Promise<AssetDetail> {
  return apiClient.get<AssetDetail>(`/v1/Assets/${id}?companyId=${companyId}`)
}

export async function runDepreciation(companyId: string, asOfDate: string): Promise<any> {
  const idempotencyKey = crypto.randomUUID()
  return apiClient.post('/v1/Assets/depreciation-run', { companyId, asOfDate }, {
    headers: { 'Idempotency-Key': idempotencyKey }
  })
}

export async function capitalizeAsset(id: string, companyId: string, capitalizationDate: string): Promise<any> {
  const idempotencyKey = crypto.randomUUID()
  return apiClient.post(`/v1/Assets/${id}/capitalize`, { companyId, capitalizationDate }, {
    headers: { 'Idempotency-Key': idempotencyKey }
  })
}
