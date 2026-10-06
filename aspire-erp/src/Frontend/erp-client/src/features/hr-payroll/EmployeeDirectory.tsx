import { Users } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Pagination } from '../../components/ui/Pagination'
import { usePagination } from '../../lib/pagination'
import { isEligibleForPeriod, type EmploymentStatus } from './types'
import { useEmployees } from './useHrPayrollData'

const STATUS_BADGE: Record<EmploymentStatus, string> = {
  Active: 'bg-emerald-100 text-emerald-700',
  Inactive: 'bg-slate-100 text-slate-500',
  Suspended: 'bg-amber-100 text-amber-800',
  Left: 'bg-rose-100 text-rose-700',
}

/**
 * Employee directory (Task 12.5): live rows from GET /api/v1/hr/employees with the
 * lifecycle status and the HR-03 eligibility badge for the workbench period. No mocks -
 * every row is a real master (SQL-seeded like BOMs); creates/updates stay out of scope.
 */
export function EmployeeDirectory({
  companyId,
  periodStart,
  periodEnd,
}: {
  companyId: string
  periodStart: string
  periodEnd: string
}) {
  const paging = usePagination()
  const employeesQuery = useEmployees(companyId, paging.page, paging.pageSize)
  const { t } = useTranslation('hr-payroll')

  if (employeesQuery.status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          {t('directory.loadFailed')}
          {employeesQuery.error?.status ? ` (HTTP ${employeesQuery.error.status})` : ''}.
        </p>
        {employeesQuery.error?.message ? <p className="mt-1">{employeesQuery.error.message}</p> : null}
        <button
          type="button"
          onClick={() => employeesQuery.reload()}
          className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
        >
          {t('directory.retry')}
        </button>
      </div>
    )
  }

  if (employeesQuery.status !== 'success') {
    return <p className="px-1 py-3 text-sm text-slate-500">{t('directory.loading')}</p>
  }

  // Both counts describe the visible page (the paginator below carries the global total).
  const eligible = employeesQuery.items.filter((e) => isEligibleForPeriod(e, periodStart, periodEnd)).length

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h3 className="flex items-center gap-2 text-base font-semibold text-slate-900">
            <Users className="h-4 w-4 text-indigo-600" />
            {t('directory.title')}
          </h3>
          <p className="text-xs text-slate-500">
            {t('directory.summary', { total: employeesQuery.items.length, eligible, start: periodStart, end: periodEnd })}
          </p>
        </div>
      </div>

      {employeesQuery.items.length === 0 ? (
        <p className="py-4 text-center text-sm text-slate-500">{t('directory.empty')}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">{t('directory.colEmployee')}</th>
                <th className="py-2.5 font-semibold">{t('directory.colEmail')}</th>
                <th className="py-2.5 font-semibold">{t('directory.colJoined')}</th>
                <th className="py-2.5 font-semibold">{t('directory.colStatus')}</th>
                <th className="py-2.5 text-right font-semibold">{t('directory.colEligibility')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {employeesQuery.items.map((employee) => {
                const eligibleForPeriod = isEligibleForPeriod(employee, periodStart, periodEnd)
                return (
                  <tr key={employee.id} className="h-11 hover:bg-slate-50">
                    <td className="py-2">
                      <span className="font-medium text-slate-900">
                        {employee.firstName} {employee.lastName}
                      </span>{' '}
                      <span className="font-mono text-slate-400">{employee.employeeNumber}</span>
                    </td>
                    <td className="py-2 text-slate-600">{employee.workEmail}</td>
                    <td className="py-2 font-mono text-slate-600">{employee.dateOfJoining}</td>
                    <td className="py-2">
                      <span
                        className={`rounded px-2 py-0.5 text-[11px] font-medium ${STATUS_BADGE[employee.status]}`}
                      >
                        {employee.status}
                      </span>
                    </td>
                    <td className="py-2 text-right">
                      <span
                        className={`rounded px-2 py-0.5 text-[11px] font-medium ${
                          eligibleForPeriod
                            ? 'bg-emerald-100 text-emerald-800'
                            : 'bg-slate-100 text-slate-500'
                        }`}
                        title={eligibleForPeriod ? t('directory.eligibleTitle') : t('directory.skippedTitle')}
                      >
                        {eligibleForPeriod ? t('directory.eligible') : t('directory.skipped')}
                      </span>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="mt-3 flex justify-end">
        <Pagination
          totalCount={employeesQuery.totalCount}
          page={paging.page}
          pageSize={paging.pageSize}
          onPageChange={paging.setPage}
        />
      </div>
    </div>
  )
}
