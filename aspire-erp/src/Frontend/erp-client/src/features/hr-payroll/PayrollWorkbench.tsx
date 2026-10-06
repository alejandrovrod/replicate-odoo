import { ArrowRight, BadgeDollarSign, CalendarRange } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Pagination } from '../../components/ui/Pagination'
import { useErpAction } from '../../lib/useErpAction'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { formatMoney } from '../../lib/format'
import { usePagination } from '../../lib/pagination'
import type { PayrollEntryStatus, PayrollRun } from './types'
import {
  payrollPeriodDefaults,
  postCancelPayrollRun,
  postDisbursePayrollRun,
  postSubmitPayrollRun,
  usePayrollRunDetail,
  usePayrollRuns,
} from './useHrPayrollData'

const STATUS_BADGE: Record<PayrollEntryStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  Submitted: 'bg-sky-100 text-sky-700',
  Paid: 'bg-emerald-100 text-emerald-700',
  Cancelled: 'bg-rose-100 text-rose-700',
}

const todayIso = () => new Date().toISOString().slice(0, 10)

/**
 * Payroll workbench (Task 12.5): the monthly run flow over the real API - period picker,
 * submit (slips + accrual voucher, atomic), disburse (Dr 2150 / Cr bank), cancel (accrual
 * mirror), and the slip detail with itemized lines. Every mutation posts with a FRESH
 * `Idempotency-Key` and reloads the runs, which is what makes the new status appear live.
 */
export function PayrollWorkbench({
  companyId,
  periodStart,
  periodEnd,
  onPeriodChange,
}: {
  companyId: string
  periodStart: string
  periodEnd: string
  onPeriodChange: (start: string, end: string) => void
}) {
  const paging = usePagination()
  const runsQuery = usePayrollRuns(companyId, paging.page, paging.pageSize)
  const { t, i18n } = useTranslation('hr-payroll')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [postingDate, setPostingDate] = useState(todayIso())
  const [bankAccountId, setBankAccountId] = useState('')

  const detailQuery = usePayrollRunDetail(companyId, selectedId)

  const refresh = () => {
    runsQuery.reload()
    detailQuery.reload()
  }

  const { state: submitState, dispatch: submit, isPending: isSubmitting } = useErpAction<
    Awaited<ReturnType<typeof postSubmitPayrollRun>>,
    void
  >(async () => {
    const result = await postSubmitPayrollRun(companyId, periodStart, periodEnd, postingDate)
    setSelectedId(result.entry.id)
    refresh()
    return result
  })

  const { state: disburseState, dispatch: disburse, isPending: isDisbursing } = useErpAction<
    PayrollRun,
    string
  >(async (entryId) => {
    const run = await postDisbursePayrollRun(entryId, companyId, bankAccountId.trim(), postingDate)
    refresh()
    return run
  })

  const { state: cancelState, dispatch: cancel, isPending: isCancelling } = useErpAction<
    PayrollRun,
    string
  >(async (entryId) => {
    const run = await postCancelPayrollRun(entryId, companyId, postingDate)
    refresh()
    return run
  })

  const lastError = submitState.error ?? disburseState.error ?? cancelState.error
  const lastErrorCode = submitState.errorCode ?? disburseState.errorCode ?? cancelState.errorCode

  if (runsQuery.status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          {t('workbench.loadFailed')}
          {runsQuery.error?.status ? ` (HTTP ${runsQuery.error.status})` : ''}.
        </p>
        {runsQuery.error?.message ? <p className="mt-1">{runsQuery.error.message}</p> : null}
        <button
          type="button"
          onClick={() => runsQuery.reload()}
          className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
        >
          {t('workbench.retry')}
        </button>
      </div>
    )
  }

  if (runsQuery.status !== 'success') {
    return <p className="px-1 py-3 text-sm text-slate-500">{t('workbench.loading')}</p>
  }

  return (
    <div className="space-y-4">
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h3 className="flex items-center gap-2 text-base font-semibold text-slate-900">
              <CalendarRange className="h-4 w-4 text-indigo-600" />
              {t('workbench.title')}
            </h3>
            <p className="text-xs text-slate-500">{t('workbench.subtitle')}</p>
          </div>
        </div>

        <div className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-xs font-medium text-slate-600">
            {t('workbench.periodStart')}
            <input
              type="date"
              value={periodStart}
              onChange={(e) => onPeriodChange(e.target.value, periodEnd)}
              className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 font-mono text-xs"
            />
          </label>
          <label className="flex flex-col gap-1 text-xs font-medium text-slate-600">
            {t('workbench.periodEnd')}
            <input
              type="date"
              value={periodEnd}
              onChange={(e) => onPeriodChange(periodStart, e.target.value)}
              className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 font-mono text-xs"
            />
          </label>
          <label className="flex flex-col gap-1 text-xs font-medium text-slate-600">
            {t('workbench.postingDate')}
            <input
              type="date"
              value={postingDate}
              onChange={(e) => setPostingDate(e.target.value)}
              className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 font-mono text-xs"
            />
          </label>
          <button
            type="button"
            disabled={isSubmitting || !companyId}
            onClick={() => submit()}
            className="flex items-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            <BadgeDollarSign className="h-3.5 w-3.5" />
            {isSubmitting ? t('workbench.submitting') : t('workbench.submit')}
          </button>
          <button
            type="button"
            onClick={() => {
              const defaults = payrollPeriodDefaults()
              onPeriodChange(defaults.start, defaults.end)
              setPostingDate(todayIso())
            }}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100"
          >
            {t('workbench.thisMonth')}
          </button>
        </div>

        {submitState.data ? (
          <div className="mt-4 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-xs text-emerald-900">
            <p className="font-semibold">
              {t('workbench.submitted', {
                number: submitState.data.entry.payrollNumber,
                count: submitState.data.createdSlipCount,
                gross: formatMoney(submitState.data.entry.totalGrossPay),
                deductions: formatMoney(submitState.data.entry.totalDeductions),
                net: formatMoney(submitState.data.entry.totalNetPay),
                voucher: submitState.data.entry.accrualVoucherNo
                  ? t('workbench.voucherSuffix', { voucher: submitState.data.entry.accrualVoucherNo })
                  : '',
              })}
            </p>
            {submitState.data.skipped.length > 0 ? (
              <ul className="mt-2 list-disc pl-5">
                {submitState.data.skipped.map((skip) => (
                  <li key={skip.employeeId}>
                    <span className="font-mono">{skip.employeeNumber}</span>{' '}
                    {t('workbench.skippedSuffix')}: {skip.reason}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-1">{t('workbench.skippedNone')}</p>
            )}
          </div>
        ) : null}

        {lastError ? (
          <p className="mt-3 rounded-md border border-rose-300 bg-rose-50 px-4 py-2.5 text-xs text-rose-800">
            {translateErrorCode(i18n, lastErrorCode, lastError)}
          </p>
        ) : null}
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="mb-1 text-base font-semibold text-slate-900">{t('workbench.runsTitle')}</h3>
        <p className="mb-4 text-xs text-slate-500">{t('workbench.runsSubtitle')}</p>

        {runsQuery.items.length === 0 ? (
          <p className="py-4 text-center text-sm text-slate-500">{t('workbench.empty')}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="border-b border-slate-200 text-slate-400">
                  <th className="py-2.5 font-semibold">{t('workbench.colRun')}</th>
                  <th className="py-2.5 font-semibold">{t('workbench.colPeriod')}</th>
                  <th className="py-2.5 font-semibold">{t('workbench.colNetPay')}</th>
                  <th className="py-2.5 font-semibold">{t('workbench.colSlips')}</th>
                  <th className="py-2.5 font-semibold">{t('workbench.colStatus')}</th>
                  <th className="py-2.5 text-right font-semibold">{t('workbench.colActions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {runsQuery.items.map((run) => (
                  <tr key={run.id} className="h-11 hover:bg-slate-50">
                    <td className="py-2">
                      <button
                        type="button"
                        onClick={() => setSelectedId(run.id === selectedId ? null : run.id)}
                        className="font-mono font-medium text-sky-700 hover:underline"
                      >
                        {run.payrollNumber}
                      </button>
                    </td>
                    <td className="py-2 font-mono text-slate-600">
                      {run.startDate}…{run.endDate}
                    </td>
                    <td className="py-2 font-mono">{formatMoney(run.totalNetPay)}</td>
                    <td className="py-2 font-mono">{run.slipCount}</td>
                    <td className="py-2">
                      <span
                        className={`rounded px-2 py-0.5 text-[11px] font-medium ${STATUS_BADGE[run.status]}`}
                      >
                        {run.status}
                      </span>
                    </td>
                    <td className="py-2">
                      <div className="flex justify-end gap-1.5">
                        {run.status === 'Submitted' ? (
                          <>
                            <ActionButton
                              label={t('workbench.disburse')}
                              title={t('workbench.disburseTitle')}
                              pending={isDisbursing}
                              disabled={bankAccountId.trim() === ''}
                              onClick={() => disburse(run.id)}
                            />
                            <ActionButton
                              label={t('workbench.cancel')}
                              title={t('workbench.cancelTitle')}
                              pending={isCancelling}
                              danger
                              onClick={() => cancel(run.id)}
                            />
                          </>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="mt-3 flex justify-end">
          <Pagination
            totalCount={runsQuery.totalCount}
            page={paging.page}
            pageSize={paging.pageSize}
            onPageChange={paging.setPage}
          />
        </div>

        <div className="mt-4 flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-xs font-medium text-slate-600">
            {t('workbench.bankAccount')}
            <input
              value={bankAccountId}
              onChange={(e) => setBankAccountId(e.target.value)}
              placeholder={t('workbench.bankPlaceholder')}
              spellCheck={false}
              className="w-72 rounded-lg border border-slate-300 bg-white px-3 py-1.5 font-mono text-xs"
            />
          </label>
          <span className="pb-2 text-[11px] text-slate-400">{t('workbench.wiredHint')}</span>
        </div>

        {detailQuery.status === 'error' ? (
          <p className="mt-4 rounded-md border border-rose-300 bg-rose-50 px-4 py-2.5 text-xs text-rose-800">
            {t('workbench.detailFailed')}
            {detailQuery.error?.status ? ` (HTTP ${detailQuery.error.status})` : ''}.
          </p>
        ) : null}

        {detailQuery.status === 'loading' ? (
          <p className="mt-4 text-xs text-slate-500">{t('workbench.detailLoading')}</p>
        ) : null}

        {detailQuery.data ? (
          <div className="mt-4 space-y-3">
            <p className="flex items-center gap-1.5 text-xs text-slate-500">
              <ArrowRight className="h-3.5 w-3.5" />
              {t('workbench.detailAccrual', {
                number: detailQuery.data.entry.payrollNumber,
                voucher: detailQuery.data.entry.accrualVoucherNo ?? '—',
              })}
              {detailQuery.data.entry.paymentVoucherNo
                ? t('workbench.detailPayment', { voucher: detailQuery.data.entry.paymentVoucherNo })
                : ''}
              .
            </p>
            {detailQuery.data.slips.map((slip) => (
              <div key={slip.id} className="rounded-lg border border-slate-200 p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="font-mono text-xs font-semibold text-slate-900">{slip.slipNumber}</p>
                  <span
                    className={`rounded px-2 py-0.5 text-[11px] font-medium ${
                      slip.status === 'Submitted'
                        ? 'bg-sky-100 text-sky-700'
                        : slip.status === 'Cancelled'
                          ? 'bg-rose-100 text-rose-700'
                          : 'bg-slate-100 text-slate-700'
                    }`}
                  >
                    {slip.status}
                  </span>
                </div>
                <p className="mt-1 font-mono text-xs text-slate-600">
                  {t('workbench.slipLine', {
                    gross: formatMoney(slip.grossPay),
                    deductions: formatMoney(slip.totalDeductions),
                    net: formatMoney(slip.netPay),
                    paid: slip.paymentDays,
                    absent: slip.absentDays,
                  })}
                </p>
                <table className="mt-2 w-full text-left text-xs">
                  <thead>
                    <tr className="border-b border-slate-100 text-slate-400">
                      <th className="py-1.5 font-semibold">{t('workbench.colComponent')}</th>
                      <th className="py-1.5 font-semibold">{t('workbench.colType')}</th>
                      <th className="py-1.5 text-right font-semibold">{t('workbench.colAmount')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-50">
                    {slip.lines.map((line) => (
                      <tr key={line.id}>
                        <td className="py-1.5 text-slate-700">{line.componentName}</td>
                        <td className="py-1.5 text-slate-500">{line.componentType}</td>
                        <td className="py-1.5 text-right font-mono text-slate-700">{formatMoney(line.amount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
          </div>
        ) : null}
      </div>
    </div>
  )
}

function ActionButton({
  label,
  title,
  pending,
  danger,
  disabled,
  onClick,
}: {
  label: string
  title: string
  pending: boolean
  danger?: boolean
  disabled?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      title={title}
      disabled={pending || disabled}
      onClick={onClick}
      className={`rounded px-2.5 py-1 text-[11px] font-semibold disabled:opacity-50 ${
        danger
          ? 'border border-rose-300 text-rose-700 hover:bg-rose-50'
          : 'border border-slate-300 text-slate-700 hover:bg-slate-100'
      }`}
    >
      {label}
    </button>
  )
}
