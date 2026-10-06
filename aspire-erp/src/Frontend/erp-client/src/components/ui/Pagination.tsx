import { useTranslation } from 'react-i18next'

interface PaginationProps {
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
}

/**
 * Universal paginator (Standard Pagination Pattern): every flat list view renders this under
 * its table, wired to the same `totalCount`/`page`/`pageSize` contract the backend envelope
 * returns. Page numbers collapse with ellipsis past 7 buttons; a single page renders only
 * the "Page X of Y" label.
 */
export function Pagination({ totalCount, page, pageSize, onPageChange }: PaginationProps) {
  const { t } = useTranslation('common')

  const totalPages = Math.max(1, Math.ceil(totalCount / Math.max(1, pageSize)))
  const current = Math.min(Math.max(1, page), totalPages)

  const pages: (number | '…')[] = buildPageWindow(current, totalPages)

  const buttonClass = (active: boolean, disabled = false) =>
    `rounded-md border px-2.5 py-1 text-xs font-medium transition-colors ${
      active
        ? 'border-indigo-600 bg-indigo-600 text-white'
        : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
    } ${disabled ? 'cursor-not-allowed opacity-40 hover:bg-white' : ''}`

  return (
    <nav aria-label={t('pagination.page', { page: current, total: totalPages })} className="flex flex-wrap items-center gap-1.5">
      <button
        type="button"
        disabled={current <= 1}
        onClick={() => onPageChange(current - 1)}
        className={buttonClass(false, current <= 1)}
      >
        {t('pagination.previous')}
      </button>

      {pages.map((entry, index) =>
        entry === '…'
          ? (
              <span key={`gap-${index}`} className="px-1 text-xs text-slate-400" aria-hidden="true">
                …
              </span>
            )
          : (
              <button
                key={entry}
                type="button"
                aria-current={entry === current ? 'page' : undefined}
                onClick={() => onPageChange(entry)}
                className={buttonClass(entry === current)}
              >
                {entry}
              </button>
            ),
      )}

      <button
        type="button"
        disabled={current >= totalPages}
        onClick={() => onPageChange(current + 1)}
        className={buttonClass(false, current >= totalPages)}
      >
        {t('pagination.next')}
      </button>

      <span className="ml-2 text-xs text-slate-500">
        {t('pagination.page', { page: current, total: totalPages })}
      </span>
    </nav>
  )
}

/** Sliding window of page numbers: first, last, and ±2 around current, with gaps collapsed. */
function buildPageWindow(current: number, totalPages: number): (number | '…')[] {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, i) => i + 1)
  }

  const keep = new Set([1, 2, totalPages - 1, totalPages, current - 1, current, current + 1])
  const ordered = [...keep].filter((n) => n >= 1 && n <= totalPages).sort((a, b) => a - b)

  const window: (number | '…')[] = []
  let previous = 0
  for (const n of ordered) {
    if (n - previous > 1) window.push('…')
    window.push(n)
    previous = n
  }
  return window
}
