import {
  Bell,
  Building2,
  CheckCircle2,
  Search,
  ShieldCheck,
  User,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { type NavRoute, useNavigationStore } from '../../store/useNavigationStore'
import { useTenantStore } from '../../store/useTenantStore'
import type { EnCommonKeys } from '../../types/i18n.generated'
import { LanguageSelector } from './LanguageSelector'

/**
 * Route chrome is a translation map, not a string table: `Header` is the first surface a user
 * sees, so it has to flip with the language (spec 00-i18n - F-10 observability, plus these
 * labels were not owned by any task in the original breakdown). Keys are typed as
 * `EnCommonKeys`, so a typo fails `tsc -b`.
 */
const ROUTE_NAV: Record<NavRoute, { title: EnCommonKeys; category: EnCommonKeys }> = {
  dashboard: { title: 'nav.route.dashboard', category: 'nav.category.dashboard' },
  'accounting-coa': { title: 'nav.route.accounting-coa', category: 'nav.category.accounting' },
  'accounting-journal': {
    title: 'nav.route.accounting-journal',
    category: 'nav.category.accounting',
  },
  'assets-categories': { title: 'nav.item.assetsCategories', category: 'nav.category.accounting' },
  banking: { title: 'nav.route.banking', category: 'nav.category.treasury' },
  stock: { title: 'nav.route.stock', category: 'nav.category.stock' },
  manufacturing: { title: 'nav.route.manufacturing', category: 'nav.category.manufacturing' },
  selling: { title: 'nav.route.selling', category: 'nav.category.selling' },
  buying: { title: 'nav.route.buying', category: 'nav.category.buying' },
  'hr-payroll': { title: 'nav.route.hr-payroll', category: 'nav.category.hr' },
  crm: { title: 'nav.route.crm', category: 'nav.category.crm' },
  'stock-warehouses': { title: 'nav.route.stock', category: 'nav.category.stock' },
  'assets': { title: 'nav.item.assets', category: 'nav.category.accounting' },
  'stock-items': { title: 'nav.route.stock', category: 'nav.category.stock' },
  'selling-customers': { title: 'nav.route.selling', category: 'nav.category.selling' },
  'buying-suppliers': { title: 'nav.route.buying', category: 'nav.category.buying' },
  'accounting-currencies': { title: 'nav.item.accountingCurrencies', category: 'nav.category.accounting' },
  'banking-accounts': { title: 'nav.item.bankingAccounts', category: 'nav.category.treasury' },
}

const FALLBACK_NAV = {
  title: 'nav.fallbackTitle',
  category: 'nav.fallbackCategory',
} as const satisfies { title: EnCommonKeys; category: EnCommonKeys }

export function Header() {
  const { t } = useTranslation('common')
  const currentRoute = useNavigationStore((state) => state.currentRoute)
  const tenantId = useTenantStore((state) => state.tenantId)
  const companyId = useTenantStore((state) => state.companyId)

  const meta = ROUTE_NAV[currentRoute] ?? FALLBACK_NAV

  return (
    <header className="flex h-16 items-center justify-between border-b border-slate-200 bg-white px-6">
      {/* Breadcrumb & Title */}
      <div className="flex items-center gap-3">
        <div className="flex flex-col justify-center">
          <div className="flex items-center gap-2 text-sm font-medium text-slate-500">
            <span>{t(meta.category)}</span>
            <span className="text-slate-300">/</span>
            <span className="text-slate-900">{t(meta.title)}</span>
          </div>
        </div>
      </div>

      {/* Right Controls */}
      <div className="flex items-center gap-4">
        {/* Search Input Visual */}
        <div className="hidden items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs text-slate-400 md:flex">
          <Search className="h-3.5 w-3.5 text-slate-400" />
          <span>{t('header.searchPlaceholder')}</span>
          <kbd className="rounded border border-slate-300 bg-white px-1.5 py-0.5 font-mono text-[10px] text-slate-500">
            Ctrl+K
          </kbd>
        </div>

        {/* Tenant & Company Pill */}
        <div className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs text-slate-600">
          <Building2 className="h-3.5 w-3.5 text-sky-600" />
          <span className="font-medium text-slate-800">{t('header.company')}:</span>
          <span className="font-mono text-[11px] text-slate-500" title={companyId}>
            {companyId ? `${companyId.substring(0, 8)}...` : 'Dev Co.'}
          </span>
          <span className="text-slate-300">|</span>
          <ShieldCheck className="h-3.5 w-3.5 text-emerald-600" />
          <span className="font-mono text-[11px] text-slate-500" title={tenantId}>
            {tenantId ? `${tenantId.substring(0, 8)}...` : t('header.default')}
          </span>
        </div>

        {/* System Online Status */}
        <div className="flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-700">
          <CheckCircle2 className="h-3.5 w-3.5" />
          <span className="hidden sm:inline">{t('state.connected')}</span>
        </div>

        {/* Language Selector (spec 00-i18n F-09) */}
        <LanguageSelector />

        {/* Notifications Icon */}
        <button
          type="button"
          className="relative rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          title={t('header.notifications')}
        >
          <Bell className="h-4 w-4" />
          <span className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-sky-600" />
        </button>

        {/* User Profile */}
        <div className="flex items-center gap-2 border-l border-slate-200 pl-3">
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-slate-200 text-slate-600">
            <User className="h-4 w-4" />
          </div>
        </div>
      </div>
    </header>
  )
}
