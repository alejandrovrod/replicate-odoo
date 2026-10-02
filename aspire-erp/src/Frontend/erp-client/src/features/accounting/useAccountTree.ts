import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../api/client'
import type { AccountTreeNode } from './types'

export type AccountTreeStatus = 'idle' | 'loading' | 'success' | 'error'

interface TreeResult {
  companyId: string
  attempt: number
  status: 'success' | 'error'
  nodes: AccountTreeNode[]
  error: ApiError | null
}

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

/**
 * Loads GET /api/v1/accounts/tree for a company (Tasks 2.3/2.4 contract).
 * Tenant header injection and ProblemDetails handling live in `src/api/client.ts`.
 *
 * Every `setState` happens inside a promise callback, never synchronously in the effect:
 * the request status is *derived* by comparing the loaded `(companyId, attempt)` against the
 * current inputs, so a changed company or a retry reads as `loading` without a transitional
 * render pass.
 */
export function useAccountTree(companyId: string) {
  const [attempt, setAttempt] = useState(0)
  const [result, setResult] = useState<TreeResult | null>(null)

  useEffect(() => {
    if (!companyId) return

    let cancelled = false
    apiClient
      .get<AccountTreeNode[]>('/v1/accounts/tree', { params: { companyId } })
      .then(
        (response) => {
          if (cancelled) return
          setResult({ companyId, attempt, status: 'success', nodes: response.data, error: null })
        },
        (cause: unknown) => {
          if (cancelled) return
          setResult({ companyId, attempt, status: 'error', nodes: [], error: toApiError(cause) })
        },
      )

    return () => {
      cancelled = true
    }
  }, [companyId, attempt])

  const isCurrent = result !== null && result.companyId === companyId && result.attempt === attempt
  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  if (!companyId) return { nodes: [], status: 'idle' as const, error: null, reload }
  if (!isCurrent) return { nodes: [], status: 'loading' as const, error: null, reload }

  return { nodes: result.nodes, status: result.status, error: result.error, reload }
}
