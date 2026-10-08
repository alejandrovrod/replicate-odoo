import { useState, useEffect, useCallback } from 'react'
import { apiClient } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'

export interface PeriodClosingVoucher {
  id: string
  companyId: string
  voucherNo: string
  postingDate: string
  retainedEarningsAccountId: string
  documentStatus: 'Draft' | 'Submitted' | 'Cancelled'
  remarks: string
}

export function usePeriodClosing(page: number = 1, pageSize: number = 50) {
  const companyId = useTenantStore((s) => s.companyId)
  const [items, setItems] = useState<PeriodClosingVoucher[]>([])
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle')
  
  const fetchItems = useCallback(async () => {
    if (!companyId) return
    setStatus('loading')
    try {
      const res = await apiClient.get<any>(`/v1/periodclosing?companyId=${companyId}&page=${page}&pageSize=${pageSize}`)
      setItems(res.data.items || [])
      setStatus('success')
    } catch {
      setStatus('error')
    }
  }, [companyId, page, pageSize])

  useEffect(() => {
    fetchItems()
  }, [fetchItems])

  return { items, status, reload: fetchItems }
}
