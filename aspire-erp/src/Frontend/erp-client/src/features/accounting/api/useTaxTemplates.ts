import { useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'
import type { TaxCalculationBasis, TaxChargeType } from '../taxCalculation'

/** One template line (mirrors `TaxTemplateLineDto`). */
export interface TaxTemplateLine {
  id: string
  accountId: string
  accountCode: string
  accountName: string
  description: string
  rate: number
  amount: number
  chargeType: TaxChargeType
  calculationBasis: TaxCalculationBasis
}

/** One template (mirrors `TaxTemplateDto`, lines included). */
export interface TaxTemplate {
  id: string
  companyId: string
  title: string
  category: string
  isDefault: boolean
  lines: TaxTemplateLine[]
}

export interface TaxTemplatesQuery {
  templates: TaxTemplate[]
  status: 'idle' | 'loading' | 'success' | 'error'
  error: ApiError | null
  reload: () => void
}

/**
 * Active templates of one company and category (GET /api/v1/taxtemplates).
 * Array-shaped (no paging: the catalog is small), so this stays a purpose-built hook
 * rather than `useApiList` - same rationale as `useCustomFields`.
 */
export function useTaxTemplates(companyId: string, category: string): TaxTemplatesQuery {
  const [attempt, setAttempt] = useState(0)
  const [templates, setTemplates] = useState<TaxTemplate[]>([])
  const [status, setStatus] = useState<TaxTemplatesQuery['status']>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    if (!companyId || !category) {
      setTemplates([])
      setStatus('idle')
      setError(null)
      return undefined
    }
    let cancelled = false
    setStatus('loading')
    setError(null)
    apiClient
      .get<TaxTemplate[]>('/v1/taxtemplates', { params: { companyId, category } })
      .then(
        (response) => {
          if (!cancelled) {
            setTemplates(response.data)
            setStatus('success')
          }
        },
        (cause: unknown) => {
          if (!cancelled) {
            setTemplates([])
            setError(
              cause instanceof ApiError
                ? cause
                : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause)),
            )
            setStatus('error')
          }
        },
      )
    return () => {
      cancelled = true
    }
  }, [companyId, category, attempt])

  return { templates, status, error, reload: () => setAttempt((c) => c + 1) }
}
