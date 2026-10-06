import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../api/client'
import { emptyPage, type PagedResult } from './pagination'

export type QueryStatus = 'idle' | 'loading' | 'success' | 'error'

interface QueryState<T> {
  key: string
  attempt: number
  status: 'success' | 'error'
  data: PagedResult<T>
  error: ApiError | null
}

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

export interface PagedList<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  status: QueryStatus
  error: ApiError | null
  reload: () => void
}

/**
 * Shared paged GET loader (Standard Pagination Pattern): the single implementation every
 * feature list hook delegates to, so `page`/`pageSize` params, the envelope shape and the
 * reload contract behave identically everywhere. Tenant header injection and RFC 7807
 * handling live in `src/api/client.ts`.
 *
 * `params` is an object literal recreated on every render, so the effect keys off its JSON
 * round-trip (`paramsKey`) instead of its identity - keying on identity would refetch forever,
 * and keying on nothing would silently serve a stale company. The parse inside the effect is
 * what lets `params` stay out of the dependency list.
 */
export function useApiList<T>(
  path: string,
  params: Record<string, string | number | undefined>,
  enabled: boolean,
): PagedList<T> {
  const paramsKey = JSON.stringify(params)
  const key = `${path}?${paramsKey}`
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<QueryState<T> | null>(null)

  useEffect(() => {
    if (!enabled) return undefined

    let cancelled = false
    const query = JSON.parse(paramsKey) as Record<string, string | number>
    apiClient.get<PagedResult<T>>(path, { params: query }).then(
      (response) => {
        if (!cancelled) setState({ key, attempt, status: 'success', data: response.data, error: null })
      },
      (cause: unknown) => {
        if (!cancelled) {
          setState({ key, attempt, status: 'error', data: emptyPage<T>(), error: toApiError(cause) })
        }
      },
    )

    return () => {
      cancelled = true
    }
  }, [path, paramsKey, attempt, enabled, key])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  const fallback: PagedList<T> = {
    ...emptyPage<T>(),
    status: 'loading',
    error: null,
    reload,
  }

  if (!enabled) return { ...emptyPage<T>(), status: 'idle', error: null, reload }
  if (state && state.key === key && state.attempt === attempt) {
    return { ...state.data, status: state.status, error: state.error, reload }
  }
  return fallback
}

export interface TreeList<T> {
  data: T[]
  status: QueryStatus
  error: ApiError | null
  reload: () => void
}

/**
 * Array-shaped GET loader for the two documented exceptions to pagination (account tree,
 * warehouse tree): expanding a hierarchy requires full context, so these endpoints keep
 * returning the whole tree and this loader keeps the historical `T[]` contract.
 */
export function useApiTreeList<T>(
  path: string,
  params: Record<string, string | number | undefined>,
  enabled: boolean,
): TreeList<T> {
  const paramsKey = JSON.stringify(params)
  const key = `${path}?${paramsKey}`
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<{ key: string; attempt: number; status: 'success' | 'error'; data: T[]; error: ApiError | null } | null>(null)

  useEffect(() => {
    if (!enabled) return undefined

    let cancelled = false
    const query = JSON.parse(paramsKey) as Record<string, string | number>
    apiClient.get<T[]>(path, { params: query }).then(
      (response) => {
        if (!cancelled) setState({ key, attempt, status: 'success', data: response.data, error: null })
      },
      (cause: unknown) => {
        if (!cancelled) setState({ key, attempt, status: 'error', data: [], error: toApiError(cause) })
      },
    )

    return () => {
      cancelled = true
    }
  }, [path, paramsKey, attempt, enabled, key])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  if (!enabled) return { data: [], status: 'idle' as const, error: null, reload }
  if (state && state.key === key && state.attempt === attempt) {
    return { data: state.data, status: state.status, error: state.error, reload }
  }
  return { data: [], status: 'loading' as const, error: null, reload }
}
