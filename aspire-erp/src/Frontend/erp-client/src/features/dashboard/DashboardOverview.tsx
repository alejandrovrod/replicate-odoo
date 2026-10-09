import { useEffect, useMemo, useState } from 'react'
import {
  Banknote,
  DollarSign,
  Landmark,
  TrendingUp,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useNavigationStore } from '../../store/useNavigationStore'
import { useTenantStore } from '../../store/useTenantStore'
import { apiClient } from '../../api/client'
import { KpiCardsGrid, type KpiCard } from './components/KpiCardsGrid'
import { RevenueAreaChart, type MonthlyPoint } from './components/RevenueAreaChart'
import { RecentInvoicesList } from './components/RecentInvoicesList'
import { todayIso, type AgingReport, type ProfitAndLossReport } from './api/useReports'
import { useSalesInvoices } from '../selling/api/useSalesInvoices'

function monthLabel(year: number, monthIndex: number): string {
  return new Date(year, monthIndex, 1).toLocaleString(undefined, { month: 'short' })
}

function lastDayIso(year: number, monthIndex: number): string {
  return new Date(year, monthIndex + 1, 0).toISOString().slice(0, 10)
}

const money = (v: number): string =>
  v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function DashboardOverview() {
  const { t } = useTranslation('dashboard')
  const setCurrentRoute = useNavigationStore((state) => state.setCurrentRoute)
  const companyId = useTenantStore((state) => state.companyId)

  const [monthly, setMonthly] = useState<(MonthlyPoint & { net: number })[]>([])
  const [chartLoading, setChartLoading] = useState(false)
  const [aging, setAging] = useState<AgingReport | null>(null)
  const [kpiLoading, setKpiLoading] = useState(false)

  const asOf = todayIso()
  const invoicesQuery = useSalesInvoices(companyId, 1, 6)

  useEffect(() => {
    if (!companyId) return
    let cancelled = false
    setChartLoading(true)
    setKpiLoading(true)

    const now = new Date()
    const months = Array.from({ length: 6 }, (_, k) => {
      const d = new Date(now.getFullYear(), now.getMonth() - (5 - k), 1)
      return { year: d.getFullYear(), month: d.getMonth() }
    })

    const load = async () => {
      try {
        const plResults = await Promise.all(
          months.map((m) =>
            apiClient
              .get<ProfitAndLossReport>('/v1/FinancialReports/profit-and-loss', {
                params: {
                  companyId,
                  from: `${m.year}-${String(m.month + 1).padStart(2, '0')}-01`,
                  to: lastDayIso(m.year, m.month),
                },
              })
              .then((r) => r.data),
          ),
        )
        if (cancelled) return
        setMonthly(
          months.map((m, i) => ({
            label: monthLabel(m.year, m.month),
            revenue: plResults[i].revenue.total,
            expenses: plResults[i].cogs.total + plResults[i].expenses.total,
            net: plResults[i].netProfit,
          })),
        )

        const agingResponse = await apiClient.get<AgingReport>('/v1/FinancialReports/aging', {
          params: { companyId, reportDate: asOf },
        })
        if (!cancelled) setAging(agingResponse.data)
      } catch {
        if (!cancelled) {
          setMonthly([])
          setAging(null)
        }
      } finally {
        if (!cancelled) {
          setChartLoading(false)
          setKpiLoading(false)
        }
      }
    }

    load()
    return () => {
      cancelled = true
    }
    // asOf is today: refetch daily, not per render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyId])

  const current = monthly[monthly.length - 1]
  const previous = monthly.length > 1 ? monthly[monthly.length - 2] : undefined
  const revenueDelta =
    current && previous && previous.revenue !== 0
      ? ((current.revenue - previous.revenue) / Math.abs(previous.revenue)) * 100
      : 0

  const cards: KpiCard[] = useMemo(
    () => [
      {
        key: 'revenue',
        title: t('kpi.revenue'),
        value: `$${money(current?.revenue ?? 0)}`,
        sub: `${revenueDelta >= 0 ? '+' : ''}${revenueDelta.toFixed(1)}% ${t('kpi.vsLastMonth', 'vs last month')}`,
        trend: revenueDelta > 0.05 ? 'up' : revenueDelta < -0.05 ? 'down' : 'flat',
        icon: DollarSign,
        color: 'text-emerald-600 bg-emerald-50',
      },
      {
        key: 'profit',
        title: t('kpi.netProfit', 'Net Profit (MTD)'),
        value: `$${money(current?.net ?? 0)}`,
        sub: t('kpi.live', 'live'),
        trend: (current?.net ?? 0) >= 0 ? 'up' : 'down',
        icon: TrendingUp,
        color: 'text-indigo-600 bg-indigo-50',
      },
      {
        key: 'receivables',
        title: t('kpi.receivables'),
        value: `$${money(aging?.totals.receivable.outstanding ?? 0)}`,
        sub: t('kpi.live', 'live'),
        trend: 'flat',
        icon: Banknote,
        color: 'text-amber-600 bg-amber-50',
      },
      {
        key: 'payables',
        title: t('kpi.payables', 'Outstanding Payables'),
        value: `$${money(aging?.totals.payable.outstanding ?? 0)}`,
        sub: t('kpi.live', 'live'),
        trend: 'flat',
        icon: Landmark,
        color: 'text-sky-600 bg-sky-50',
      },
    ],
    [t, current, revenueDelta, aging],
  )

  return (
    <div className="space-y-6">
      <KpiCardsGrid cards={cards} loading={chartLoading || kpiLoading} />

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-5">
        <div className="xl:col-span-3">
          <RevenueAreaChart points={monthly} loading={chartLoading} />
        </div>
        <div className="xl:col-span-2">
          <RecentInvoicesList
            invoices={invoicesQuery.items}
            loading={invoicesQuery.status === 'loading'}
            onViewAll={() => setCurrentRoute('selling-invoices')}
          />
        </div>
      </div>

      {/* Quick Access & Modules */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs lg:col-span-2">
          <div className="flex items-center justify-between border-b border-slate-100 pb-4">
            <h2 className="text-base font-semibold text-slate-900">{t('modules.title')}</h2>
            <span className="rounded-full bg-sky-50 px-2.5 py-0.5 text-xs font-medium text-sky-700">
              {t('modules.badge')}
            </span>
          </div>

          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <button
              type="button"
              onClick={() => setCurrentRoute('dashboard-reports')}
              className="flex flex-col items-start rounded-lg border border-slate-200 p-4 text-left transition-all hover:border-sky-500 hover:shadow-xs"
            >
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-md bg-sky-100 text-sky-700">
                  <TrendingUp className="h-4 w-4" />
                </span>
                <span className="font-semibold text-slate-900">{t('modules.reports', 'Financial Reports')}</span>
              </div>
              <p className="mt-2 text-xs text-slate-500">{t('modules.reportsDesc', 'Balance sheet, P&L, trial balance, aging and Kardex.')}</p>
              <span className="mt-3 text-xs font-medium text-sky-600">{t('modules.reportsCta', 'Open Reports →')}</span>
            </button>

            <button
              type="button"
              onClick={() => setCurrentRoute('banking')}
              className="flex flex-col items-start rounded-lg border border-slate-200 p-4 text-left transition-all hover:border-sky-500 hover:shadow-xs"
            >
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-md bg-indigo-100 text-indigo-700">
                  <Landmark className="h-4 w-4" />
                </span>
                <span className="font-semibold text-slate-900">{t('modules.banking')}</span>
              </div>
              <p className="mt-2 text-xs text-slate-500">{t('modules.bankingDesc')}</p>
              <span className="mt-3 text-xs font-medium text-indigo-600">{t('modules.bankingCta')}</span>
            </button>

            <button
              type="button"
              onClick={() => setCurrentRoute('stock')}
              className="flex flex-col items-start rounded-lg border border-slate-200 p-4 text-left transition-all hover:border-sky-500 hover:shadow-xs"
            >
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-md bg-amber-100 text-amber-700">
                  <DollarSign className="h-4 w-4" />
                </span>
                <span className="font-semibold text-slate-900">{t('modules.stock')}</span>
              </div>
              <p className="mt-2 text-xs text-slate-500">{t('modules.stockDesc')}</p>
              <span className="mt-3 text-xs font-medium text-amber-600">{t('modules.stockCta')}</span>
            </button>

            <button
              type="button"
              onClick={() => setCurrentRoute('selling')}
              className="flex flex-col items-start rounded-lg border border-slate-200 p-4 text-left transition-all hover:border-sky-500 hover:shadow-xs"
            >
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-md bg-emerald-100 text-emerald-700">
                  <Banknote className="h-4 w-4" />
                </span>
                <span className="font-semibold text-slate-900">{t('modules.selling')}</span>
              </div>
              <p className="mt-2 text-xs text-slate-500">{t('modules.sellingDesc')}</p>
              <span className="mt-3 text-xs font-medium text-emerald-600">{t('modules.sellingCta')}</span>
            </button>
          </div>
        </div>

        {/* Activity & Specs Status */}
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
          <h2 className="text-base font-semibold text-slate-900">{t('specs.title')}</h2>
          <div className="mt-4 space-y-3">
            <div className="rounded-lg bg-slate-50 p-3 text-xs">
              <span className="font-semibold text-slate-700">{t('specs.architecture')}</span>
              <p className="text-slate-500 mt-1">{t('specs.architectureDesc')}</p>
            </div>
            <div className="rounded-lg bg-slate-50 p-3 text-xs">
              <span className="font-semibold text-slate-700">{t('specs.tenancy')}</span>
              <p className="text-slate-500 mt-1">{t('specs.tenancyDesc')}</p>
            </div>
            <div className="rounded-lg bg-slate-50 p-3 text-xs">
              <span className="font-semibold text-slate-700">{t('specs.rules')}</span>
              <p className="text-slate-500 mt-1">{t('specs.rulesDesc')}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
