import { useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'
import type { CustomFieldDefinition } from '../../../components/forms/CustomFieldsRenderer'

export type { CustomFieldDefinition }

export interface CustomFieldsQuery {
  definitions: CustomFieldDefinition[]
  status: 'idle' | 'loading' | 'success' | 'error'
  error: ApiError | null
  reload: () => void
}

/**
 * Active custom-field definitions for one root entity
 * (GET /api/v1/customfielddefinitions?entityName=). Array-shaped (no paging: the catalog
 * is small by nature), so this stays a purpose-built hook rather than `useApiList`.
 */
export function useCustomFields(entityType: string): CustomFieldsQuery {
  const [attempt, setAttempt] = useState(0)
  const [definitions, setDefinitions] = useState<CustomFieldDefinition[]>([])
  const [status, setStatus] = useState<CustomFieldsQuery['status']>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    if (!entityType) {
      setDefinitions([])
      setStatus('idle')
      setError(null)
      return undefined
    }
    let cancelled = false
    setStatus('loading')
    setError(null)
    apiClient
      .get<CustomFieldDefinition[]>('/v1/customfielddefinitions', {
        params: { entityName: entityType },
      })
      .then(
        (response) => {
          if (!cancelled) {
            setDefinitions(response.data)
            setStatus('success')
          }
        },
        (cause: unknown) => {
          if (!cancelled) {
            // A catalog read failure must never block the host form: definitions stay empty
            // (the section hides itself) and the server remains the validator of record.
            setDefinitions([])
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
  }, [entityType, attempt])

  return {
    definitions,
    status,
    error,
    reload: () => setAttempt((current) => current + 1),
  }
}
