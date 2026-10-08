import { Suspense, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { Pagination } from '../../../components/ui/Pagination'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { usePagination } from '../../../lib/pagination'
import { useTenantStore } from '../../../store/useTenantStore'
import { closeFiscalYear, useFiscalYears, type FiscalYear } from '../api/useFiscalYears'
import { FiscalYearFormModal } from './FiscalYearFormModal'
import i18n from '../../../lib/i18n'

type ClosedFilter = 'all' | 'open' | 'closed'

/**
 * Fiscal-year master (R-13 Task 5.1): company-scoped list with text search,
 * open/closed filter and Standard Pagination, create modal, and terminal close
 * with an explicit confirm step carrying the current `RowVersion`.
 */
export function FiscalYearList() {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore((s) => s.companyId)
  const paging = usePagination()

  const [filter, setFilter] = useState<ClosedFilter>('all')
  const [search, setSearch] = useState('')
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [closingId, setClosingId] = useState<string | null>(null)
  const [confirmId, setConfirmId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const isClosedParam = filter === 'all' ? undefined : filter === 'closed'

  const { items, totalCount, status, error, reload } = useFiscalYears({
    isClosed: isClosedParam,
    page: paging.page,
    pageSize: paging.pageSize,
  })

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return items
    return items.filter((y) => y.yearName.toLowerCase().includes(q))
  }, [items, search])

  const handleFilterChange = (next: ClosedFilter) => {
    setFilter(next)
    paging.reset()
  }

  const handleClose = async (year: FiscalYear) => {
    if (!companyId || closingId) return
    setClosingId(year.id)
    setActionError(null)
    try {
      await closeFiscalYear(year.id, { companyId, rowVersion: year.rowVersion })
      setConfirmId(null)
      reload()
    } catch (cause: unknown) {
      const apiError = cause instanceof ApiError ? cause : null
      setActionError(translateErrorCode(i18n, apiError?.code, apiError?.message ?? ''))
    } finally {
      setClosingId(null)
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">{t('fiscalYear.title')}</h1>
          <p className="text-sm text-slate-500">{t('fiscalYear.subtitle')}</p>
        </div>
        <Button onClick={() => setIsModalOpen(true)} disabled={!companyId}>
          {t('fiscalYear.newBtn')}
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="w-64">
          <label htmlFor="fy-search" className="sr-only">
            {t('fiscalYear.search')}
          </label>
          <Input
            id="fy-search"
            placeholder={t('fiscalYear.search')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            type="search"
          />
        </div>
        <div role="group" aria-label={t('fiscalYear.colStatus')} className="flex gap-2">
          {(['all', 'open', 'closed'] as const).map((value) => (
            <Button
              key={value}
              type="button"
              variant={filter === value ? 'default' : 'outline'}
              size="sm"
              onClick={() => handleFilterChange(value)}
              aria-pressed={filter === value}
            >
              {value === 'all'
                ? t('fiscalYear.filterAll')
                : value === 'open'
                  ? t('fiscalYear.filterOpen')
                  : t('fiscalYear.filterClosed')}
            </Button>
          ))}
        </div>
      </div>

      {actionError && (
        <p role="alert" className="text-sm text-red-600">
          {actionError}
        </p>
      )}

      <Suspense fallback={<p className="text-sm text-slate-500">{t('fiscalYear.loading')}</p>}>
        {status === 'loading' && <p className="text-sm text-slate-500">{t('fiscalYear.loading')}</p>}

        {status === 'error' && (
          <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm">
            <p className="font-medium text-red-800">{t('fiscalYear.loadFailed')}</p>
            {error?.code && (
              <p className="text-red-700">
                {t('fiscalYear.errorCode')} {translateErrorCode(i18n, error.code, error.message)}
              </p>
            )}
            <Button variant="outline" size="sm" onClick={reload} className="mt-2">
              {t('fiscalYear.retry')}
            </Button>
          </div>
        )}

        {status === 'success' && (
          <div className="rounded-lg border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-left text-sm text-slate-600">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-900">
                <tr>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('fiscalYear.colName')}
                  </th>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('fiscalYear.colStart')}
                  </th>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('fiscalYear.colEnd')}
                  </th>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('fiscalYear.colStatus')}
                  </th>
                  <th scope="col" className="px-6 py-4 font-medium">
                    <span className="sr-only">{t('periodClosing.colActions')}</span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {visible.map((year) => (
                  <tr key={year.id} className="hover:bg-slate-50">
                    <td className="px-6 py-4 font-medium text-sky-600">{year.yearName}</td>
                    <td className="px-6 py-4">{year.startDate}</td>
                    <td className="px-6 py-4">{year.endDate}</td>
                    <td className="px-6 py-4">
                      <span
                        className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                          year.isClosed ? 'bg-slate-200 text-slate-700' : 'bg-green-100 text-green-800'
                        }`}
                      >
                        {year.isClosed ? t('fiscalYear.statusClosed') : t('fiscalYear.statusOpen')}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-right">
                      {!year.isClosed &&
                        (confirmId === year.id ? (
                          <span className="inline-flex items-center gap-2">
                            <span className="text-xs text-slate-500">
                              {t('fiscalYear.closeConfirmTitle')}
                            </span>
                            <Button
                              size="sm"
                              variant="destructive"
                              disabled={closingId === year.id}
                              onClick={() => handleClose(year)}
                              aria-label={`${t('fiscalYear.confirmClose')}: ${year.yearName}`}
                            >
                              {closingId === year.id
                                ? t('fiscalYear.closing')
                                : t('fiscalYear.confirmClose')}
                            </Button>
                            <Button size="sm" variant="outline" onClick={() => setConfirmId(null)}>
                              {t('fiscalYearForm.cancel')}
                            </Button>
                          </span>
                        ) : (
                          <Button size="sm" variant="outline" onClick={() => setConfirmId(year.id)}>
                            {t('fiscalYear.closeBtn')}
                          </Button>
                        ))}
                    </td>
                  </tr>
                ))}
                {visible.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-6 py-8 text-center text-slate-500">
                      {search || filter !== 'all' ? t('fiscalYear.emptyFiltered') : t('fiscalYear.empty')}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
            <div className="border-t border-slate-200 px-6 py-4">
              <Pagination
                totalCount={totalCount}
                page={paging.page}
                pageSize={paging.pageSize}
                onPageChange={paging.setPage}
              />
            </div>
          </div>
        )}
      </Suspense>

      <FiscalYearFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSaved={reload}
      />
    </div>
  )
}
