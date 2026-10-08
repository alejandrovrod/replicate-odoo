import { Suspense, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { Pagination } from '../../../components/ui/Pagination'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { usePagination } from '../../../lib/pagination'
import { useTenantStore } from '../../../store/useTenantStore'
import { useFiscalYears } from '../api/useFiscalYears'
import {
  cancelClosingVoucher,
  submitClosingVoucher,
  useClosingPreview,
  usePeriodClosingVouchers,
  type ClosingDocumentStatus,
  type ClosingPreview,
  type PeriodClosingVoucher,
} from '../api/usePeriodClosing'
import { PeriodClosingFormModal } from './PeriodClosingFormModal'
import i18n from '../../../lib/i18n'

const formatAmount = (value: number) =>
  value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

/**
 * Closing execution screen (R-13 Task 5.2): voucher list filtered by fiscal year +
 * status with Standard Pagination, Draft creation wizard, read-only P&L preview,
 * and `Submit` / `Cancel` transitions.
 *
 * Idempotency (FC-09): one key is minted per voucher per action intent and reused
 * across retries; the in-flight guard disables the button so a double-click posts
 * once. Every §5 error code resolves through the localized `error` catalog with the
 * backend detail as fallback.
 */
export function PeriodClosingList() {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore((s) => s.companyId)
  const paging = usePagination()

  const [fiscalYearId, setFiscalYearId] = useState('')
  const [statusFilter, setStatusFilter] = useState<'' | ClosingDocumentStatus>('')
  const [search, setSearch] = useState('')
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [previewFor, setPreviewFor] = useState<string | null>(null)
  const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  // One idempotency key per voucher per intent, stable across retries (FC-09).
  const submitKeys = useRef(new Map<string, string>())
  const cancelKeys = useRef(new Map<string, string>())
  const keyFor = (cache: Map<string, string>, id: string) => {
    let key = cache.get(id)
    if (!key) {
      key = crypto.randomUUID()
      cache.set(id, key)
    }
    return key
  }

  const { items: years } = useFiscalYears({ page: 1, pageSize: 500 })
  const yearNameOf = useMemo(() => {
    const map = new Map(years.map((y) => [y.id, y.yearName] as const))
    return (id: string) => map.get(id) ?? id.slice(0, 8)
  }, [years])

  const list = usePeriodClosingVouchers({
    fiscalYearId: fiscalYearId || undefined,
    status: statusFilter || undefined,
    page: paging.page,
    pageSize: paging.pageSize,
  })

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return list.items
    return list.items.filter((v) => v.voucherNo.toLowerCase().includes(q))
  }, [list.items, search])

  const previewYearId = useMemo(() => {
    if (!previewFor) return null
    return list.items.find((v) => v.id === previewFor)?.fiscalYearId ?? null
  }, [previewFor, list.items])
  const { preview, status: previewStatus } = useClosingPreview(previewFor ? previewYearId : null)

  const resetFilters = () => {
    setFiscalYearId('')
    setStatusFilter('')
    setSearch('')
    paging.reset()
  }

  const runTransition = async (voucher: PeriodClosingVoucher, kind: 'submit' | 'cancel') => {
    if (!companyId || busyId) return
    setBusyId(voucher.id)
    setActionError(null)
    try {
      if (kind === 'submit') {
        await submitClosingVoucher(
          voucher.id,
          { companyId, rowVersion: voucher.rowVersion },
          keyFor(submitKeys.current, voucher.id),
        )
      } else {
        await cancelClosingVoucher(
          voucher.id,
          { companyId, rowVersion: voucher.rowVersion },
          keyFor(cancelKeys.current, voucher.id),
        )
      }
      setConfirmCancelId(null)
      list.reload()
    } catch (cause: unknown) {
      const apiError = cause instanceof ApiError ? cause : null
      setActionError(translateErrorCode(i18n, apiError?.code, apiError?.message ?? ''))
    } finally {
      setBusyId(null)
    }
  }

  const statusBadge = (status: ClosingDocumentStatus) => (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
        status === 'Submitted'
          ? 'bg-green-100 text-green-800'
          : status === 'Cancelled'
            ? 'bg-slate-200 text-slate-700'
            : 'bg-amber-100 text-amber-800'
      }`}
    >
      {status === 'Submitted'
        ? t('periodClosing.statusSubmitted')
        : status === 'Cancelled'
          ? t('periodClosing.statusCancelled')
          : t('periodClosing.statusDraft')}
    </span>
  )

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">
            {t('periodClosing.title')}
          </h1>
          <p className="text-sm text-slate-500">{t('periodClosing.subtitle')}</p>
        </div>
        <Button onClick={() => setIsModalOpen(true)} disabled={!companyId}>
          {t('periodClosing.newBtn')}
        </Button>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1">
          <label htmlFor="pcv-filter-year" className="text-xs font-medium text-slate-600">
            {t('periodClosing.colYear')}
          </label>
          <select
            id="pcv-filter-year"
            value={fiscalYearId}
            onChange={(e) => {
              setFiscalYearId(e.target.value)
              paging.reset()
            }}
            className="flex h-10 rounded-md border border-slate-200 bg-white px-3 text-sm"
          >
            <option value="">{t('periodClosing.filterYearAll')}</option>
            {years.map((y) => (
              <option key={y.id} value={y.id}>
                {y.yearName}
                {y.isClosed ? ` (${t('fiscalYear.statusClosed')})` : ''}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-1">
          <label htmlFor="pcv-filter-status" className="text-xs font-medium text-slate-600">
            {t('periodClosing.colStatus')}
          </label>
          <select
            id="pcv-filter-status"
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value as '' | ClosingDocumentStatus)
              paging.reset()
            }}
            className="flex h-10 rounded-md border border-slate-200 bg-white px-3 text-sm"
          >
            <option value="">{t('periodClosing.filterStatusAll')}</option>
            <option value="Draft">{t('periodClosing.statusDraft')}</option>
            <option value="Submitted">{t('periodClosing.statusSubmitted')}</option>
            <option value="Cancelled">{t('periodClosing.statusCancelled')}</option>
          </select>
        </div>
        <div className="w-64 space-y-1">
          <label htmlFor="pcv-search" className="sr-only">
            {t('periodClosing.search')}
          </label>
          <Input
            id="pcv-search"
            type="search"
            placeholder={t('periodClosing.search')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {actionError && (
        <p role="alert" className="text-sm text-red-600">
          {actionError}
        </p>
      )}

      <Suspense fallback={<p className="text-sm text-slate-500">{t('periodClosing.loading')}</p>}>
        {list.status === 'loading' && (
          <p className="text-sm text-slate-500">{t('periodClosing.loading')}</p>
        )}

        {list.status === 'error' && (
          <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm">
            <p className="font-medium text-red-800">{t('periodClosing.loadFailed')}</p>
            {list.error?.code && (
              <p className="text-red-700">
                {t('periodClosing.errorCode')}{' '}
                {translateErrorCode(i18n, list.error.code, list.error.message)}
              </p>
            )}
            <Button variant="outline" size="sm" onClick={list.reload} className="mt-2">
              {t('periodClosing.retry')}
            </Button>
          </div>
        )}

        {list.status === 'success' && (
          <div className="rounded-lg border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-left text-sm text-slate-600">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-900">
                <tr>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('periodClosing.colVoucher')}
                  </th>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('periodClosing.colYear')}
                  </th>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('periodClosing.colPostingDate')}
                  </th>
                  <th scope="col" className="px-6 py-4 font-medium">
                    {t('periodClosing.colStatus')}
                  </th>
                  <th scope="col" className="px-6 py-4 text-right font-medium">
                    {t('periodClosing.colActions')}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {visible.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50">
                    <td className="px-6 py-4 font-medium text-sky-600">{item.voucherNo}</td>
                    <td className="px-6 py-4">{yearNameOf(item.fiscalYearId)}</td>
                    <td className="px-6 py-4">{item.postingDate.split('T')[0]}</td>
                    <td className="px-6 py-4">{statusBadge(item.documentStatus)}</td>
                    <td className="px-6 py-4">
                      <span className="flex flex-wrap justify-end gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => setPreviewFor(previewFor === item.id ? null : item.id)}
                          aria-expanded={previewFor === item.id}
                        >
                          {previewFor === item.id
                            ? t('periodClosing.hidePreview')
                            : t('periodClosing.previewBtn')}
                        </Button>
                        {item.documentStatus === 'Draft' && (
                          <Button
                            size="sm"
                            disabled={busyId === item.id}
                            onClick={() => runTransition(item, 'submit')}
                            aria-label={`${t('periodClosing.submitBtn')}: ${item.voucherNo}`}
                          >
                            {busyId === item.id
                              ? t('periodClosing.submitting')
                              : t('periodClosing.submitBtn')}
                          </Button>
                        )}
                        {item.documentStatus === 'Submitted' &&
                          (confirmCancelId === item.id ? (
                            <span className="inline-flex items-center gap-2">
                              <span className="text-xs text-slate-500">
                                {t('periodClosing.cancelConfirmTitle')}
                              </span>
                              <Button
                                size="sm"
                                variant="destructive"
                                disabled={busyId === item.id}
                                onClick={() => runTransition(item, 'cancel')}
                                aria-label={`${t('periodClosing.cancelBtn')}: ${item.voucherNo}`}
                              >
                                {busyId === item.id
                                  ? t('periodClosing.cancelling')
                                  : t('periodClosing.cancelBtn')}
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => setConfirmCancelId(null)}
                              >
                                {t('periodClosing.keepBtn')}
                              </Button>
                            </span>
                          ) : (
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() => setConfirmCancelId(item.id)}
                            >
                              {t('periodClosing.cancelBtn')}
                            </Button>
                          ))}
                      </span>
                    </td>
                  </tr>
                ))}
                {visible.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-6 py-8 text-center text-slate-500">
                      {search || fiscalYearId || statusFilter
                        ? t('periodClosing.emptyFiltered')
                        : t('periodClosing.empty')}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
            <div className="border-t border-slate-200 px-6 py-4">
              <Pagination
                totalCount={list.totalCount}
                page={paging.page}
                pageSize={paging.pageSize}
                onPageChange={paging.setPage}
              />
            </div>
          </div>
        )}
      </Suspense>

      {previewFor && (
        <PreviewCard preview={preview} status={previewStatus} onClose={() => setPreviewFor(null)} />
      )}

      <PeriodClosingFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSaved={() => {
          resetFilters()
          list.reload()
        }}
      />
    </div>
  )
}

function PreviewCard({
  preview,
  status,
  onClose,
}: {
  preview: ClosingPreview | null
  status: 'idle' | 'loading' | 'success' | 'error'
  onClose: () => void
}) {
  const { t } = useTranslation('accounting')
  return (
    <section
      aria-label={t('periodClosing.previewTitle') as string}
      className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm"
    >
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-base font-semibold text-slate-900">{t('periodClosing.previewTitle')}</h2>
        <Button size="sm" variant="outline" onClick={onClose}>
          {t('periodClosing.hidePreview')}
        </Button>
      </div>
      {(status === 'loading' || status === 'idle') && (
        <p className="text-sm text-slate-500">{t('periodClosing.previewLoading')}</p>
      )}
      {status === 'success' && preview && preview.lines.length === 0 && (
        <p role="status" className="rounded-md bg-amber-50 p-3 text-sm text-amber-800">
          {t('periodClosing.previewEmpty')}
        </p>
      )}
      {status === 'success' && preview && preview.lines.length > 0 && (
        <>
          <div className="overflow-hidden rounded-md border border-slate-200">
            <table className="w-full text-left text-sm text-slate-600">
              <thead className="bg-slate-50 text-slate-900">
                <tr>
                  <th scope="col" className="px-4 py-2 font-medium">
                    {t('periodClosing.previewColAccount')}
                  </th>
                  <th scope="col" className="px-4 py-2 text-right font-medium">
                    {t('periodClosing.previewColDebit')}
                  </th>
                  <th scope="col" className="px-4 py-2 text-right font-medium">
                    {t('periodClosing.previewColCredit')}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {preview.lines.map((line) => (
                  <tr key={line.accountId}>
                    <td className="px-4 py-2">
                      {line.code} - {line.name}
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums">
                      {formatAmount(line.debit)}
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums">
                      {formatAmount(line.credit)}
                    </td>
                  </tr>
                ))}
                {preview.retainedLine && (
                  <tr className="bg-sky-50 font-medium">
                    <td className="px-4 py-2">
                      {preview.retainedLine.code} - {preview.retainedLine.name}{' '}
                      <span className="ml-1 rounded-full bg-sky-100 px-2 py-0.5 text-[10px] text-sky-800">
                        {t('periodClosing.previewRetainedBadge')}
                      </span>
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums">
                      {formatAmount(preview.retainedLine.debit)}
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums">
                      {formatAmount(preview.retainedLine.credit)}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <p role="status" className="mt-3 text-sm font-medium text-slate-800">
            {preview.net > 0
              ? t('periodClosing.previewNetProfit', { amount: formatAmount(preview.net) })
              : preview.net < 0
                ? t('periodClosing.previewNetLoss', { amount: formatAmount(Math.abs(preview.net)) })
                : t('periodClosing.previewNetZero')}
          </p>
        </>
      )}
    </section>
  )
}
