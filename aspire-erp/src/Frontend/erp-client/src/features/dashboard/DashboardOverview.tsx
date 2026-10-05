import {
  ArrowDownRight,
  ArrowUpRight,
  Banknote,
  DollarSign,
  Landmark,
  Package,
  TrendingUp,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useNavigationStore } from '../../store/useNavigationStore'
import type { EnDashboardKeys } from '../../types/i18n.generated'

export function DashboardOverview() {
  const { t } = useTranslation('dashboard')
  const setCurrentRoute = useNavigationStore((state) => state.setCurrentRoute)

  // Titles are keys (typed, so a typo fails the build); values and deltas are mock
  // demo data and stay literal until a real KPI endpoint exists.
  const kpis: { title: EnDashboardKeys; value: string; change: string; trend: 'up' | 'down'; icon: typeof DollarSign; color: string }[] = [
    {
      title: 'kpi.revenue',
      value: '$124,500.00',
      change: '+14.2%',
      trend: 'up',
      icon: DollarSign,
      color: 'text-emerald-600 bg-emerald-50',
    },
    {
      title: 'kpi.cash',
      value: '$84,320.50',
      change: '+3.8%',
      trend: 'up',
      icon: Landmark,
      color: 'text-sky-600 bg-sky-50',
    },
    {
      title: 'kpi.receivables',
      value: '$32,150.00',
      change: '-5.1%',
      trend: 'down',
      icon: Banknote,
      color: 'text-amber-600 bg-amber-50',
    },
    {
      title: 'kpi.stockValuation',
      value: '$210,480.00',
      change: '+8.4%',
      trend: 'up',
      icon: Package,
      color: 'text-indigo-600 bg-indigo-50',
    },
  ]

  return (
    <div className="space-y-6">
      {/* KPI Cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {kpis.map((kpi) => {
          const Icon = kpi.icon
          return (
            <div key={kpi.title} className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="text-xs font-medium text-slate-500">{t(kpi.title)}</span>
                <div className={`rounded-lg p-2 ${kpi.color}`}>
                  <Icon className="h-5 w-5" />
                </div>
              </div>
              <div className="mt-4 flex items-baseline justify-between">
                <span className="text-2xl font-bold tracking-tight text-slate-900">{kpi.value}</span>
                <span
                  className={`flex items-center text-xs font-semibold ${
                    kpi.trend === 'up' ? 'text-emerald-600' : 'text-rose-600'
                  }`}
                >
                  {kpi.trend === 'up' ? (
                    <ArrowUpRight className="mr-0.5 h-3.5 w-3.5" />
                  ) : (
                    <ArrowDownRight className="mr-0.5 h-3.5 w-3.5" />
                  )}
                  {kpi.change}
                </span>
              </div>
            </div>
          )
        })}
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
              onClick={() => setCurrentRoute('accounting-coa')}
              className="flex flex-col items-start rounded-lg border border-slate-200 p-4 text-left transition-all hover:border-sky-500 hover:shadow-xs"
            >
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-md bg-sky-100 text-sky-700">
                  <TrendingUp className="h-4 w-4" />
                </span>
                <span className="font-semibold text-slate-900">{t('modules.coa')}</span>
              </div>
              <p className="mt-2 text-xs text-slate-500">{t('modules.coaDesc')}</p>
              <span className="mt-3 text-xs font-medium text-sky-600">{t('modules.coaCta')}</span>
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
                  <Package className="h-4 w-4" />
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
                  <DollarSign className="h-4 w-4" />
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
