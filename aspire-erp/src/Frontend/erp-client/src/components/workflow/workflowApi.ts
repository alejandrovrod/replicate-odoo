import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../api/client'

/** One executable move (mirrors `AvailableTransitionDto`). */
export interface AvailableTransition {
  toState: string
  actionLabel: string
  allowedRole: string
  allowedForCaller: boolean
}

/** Current operational state plus moves (mirrors `WorkflowStatusDto`). */
export interface WorkflowStatus {
  documentId: string
  documentType: string
  currentState: string
  transitions: AvailableTransition[]
}

export interface WorkflowStatusQuery {
  status: WorkflowStatus | null
  /** True when no flow governs the document (bar hides itself). */
  hasFlow: boolean
  loading: boolean
  error: ApiError | null
  reload: () => void
}

/**
 * Operational status of one document (GET /api/v1/workflows/status). A 404 means no
 * active flow governs the type (or the document is unknown) - the caller renders
 * nothing instead of failing the host view.
 */
export function useWorkflowStatus(
  documentType: string,
  companyId: string,
  documentId: string | null,
): WorkflowStatusQuery {
  const [attempt, setAttempt] = useState(0)
  const [status, setStatus] = useState<WorkflowStatus | null>(null)
  const [hasFlow, setHasFlow] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    if (!documentType || !companyId || !documentId) {
      setStatus(null)
      setHasFlow(false)
      setLoading(false)
      setError(null)
      return undefined
    }
    let cancelled = false
    setLoading(true)
    setError(null)
    apiClient
      .get<WorkflowStatus>('/v1/workflows/status', {
        params: { documentType, companyId, documentId },
      })
      .then(
        (response) => {
          if (!cancelled) {
            setStatus(response.data)
            setHasFlow(true)
            setLoading(false)
          }
        },
        (cause: unknown) => {
          if (!cancelled) {
            // No flow (or unknown document): hide the bar, keep the host usable.
            setStatus(null)
            setHasFlow(false)
            setLoading(false)
            setError(
              cause instanceof ApiError
                ? cause
                : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause)),
            )
          }
        },
      )
    return () => {
      cancelled = true
    }
  }, [documentType, companyId, documentId, attempt])

  const reload = useCallback(() => setAttempt((c) => c + 1), [])

  return { status, hasFlow, loading, error, reload }
}

/** Executes one operational move (POST /api/v1/workflows/transition). */
export async function applyWorkflowTransition(payload: {
  documentType: string
  companyId: string
  documentId: string
  toState: string
}): Promise<WorkflowStatus> {
  const response = await apiClient.post<WorkflowStatus>('/v1/workflows/transition', {
    documentType: payload.documentType,
    companyId: payload.companyId,
    documentId: payload.documentId,
    toState: payload.toState,
  })
  return response.data
}
