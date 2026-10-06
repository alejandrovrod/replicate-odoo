import { useCallback, useState } from 'react'

/**
 * Frontend mirror of the backend `PagedResult<T>` envelope (Standard Pagination Pattern).
 * Property names match the API's camelCase JSON (`items`, `totalCount`, `pageNumber`,
 * `pageSize`, `totalPages`).
 */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
}

/** Query params every list endpoint accepts (`page` is 1-based). */
export interface PaginationParams {
  page?: number
  pageSize?: number
}

/** Default page size used when a view does not choose its own. */
export const DEFAULT_PAGE_SIZE = 50

/** Largest page the API accepts — used by pickers that need the whole catalog on one page. */
export const MAX_PAGE_SIZE = 500

/** Empty page for idle/loading states (never null, so views render unconditionally). */
export function emptyPage<T>(): PagedResult<T> {
  return { items: [], totalCount: 0, pageNumber: 1, pageSize: DEFAULT_PAGE_SIZE, totalPages: 0 }
}

/**
 * Local page state for a view. Pass `paging.params` into the data hook and spread the rest
 * into `<Pagination totalCount={...} />`; call `reset()` when a filter changes so the view
 * jumps back to page one instead of stranding on an empty page.
 */
export function usePagination(initialPageSize: number = DEFAULT_PAGE_SIZE) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(initialPageSize)

  const handlePageSizeChange = useCallback((next: number) => {
    setPageSize(next)
    setPage(1)
  }, [])

  const reset = useCallback(() => setPage(1), [])

  return {
    page,
    pageSize,
    params: { page, pageSize } as PaginationParams,
    setPage,
    setPageSize: handlePageSizeChange,
    reset,
  }
}
