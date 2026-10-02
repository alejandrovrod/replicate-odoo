import {
  Bell,
  Building2,
  CheckCircle2,
  Search,
  ShieldCheck,
  User,
} from 'lucide-react'
import { type NavRoute, useNavigationStore } from '../../store/useNavigationStore'
import { useTenantStore } from '../../store/useTenantStore'

const ROUTE_TITLES: Record<NavRoute, { title: string; category: string }> = {
  dashboard: { title: 'Executive Overview', category: 'Dashboard' },
  'accounting-coa': { title: 'Chart of Accounts', category: 'Accounting' },
  'accounting-journal': { title: 'General Ledger Entries', category: 'Accounting' },
  banking: { title: 'Bank Reconciliation & Feeds', category: 'Treasury' },
  stock: { title: 'Inventory & Warehouses (Kardex)', category: 'Stock' },
  selling: { title: 'Sales Invoices & POS', category: 'Selling' },
  buying: { title: 'Purchase Orders & Vendor Bills', category: 'Buying' },
}

export function Header() {
  const currentRoute = useNavigationStore((state) => state.currentRoute)
  const tenantId = useTenantStore((state) => state.tenantId)
  const companyId = useTenantStore((state) => state.companyId)

  const meta = ROUTE_TITLES[currentRoute] || { title: 'Module', category: 'ERP' }

  return (
    <header className="flex h-16 items-center justify-between border-b border-slate-200 bg-white px-6">
      {/* Breadcrumb & Title */}
      <div className="flex items-center gap-3">
        <div className="flex flex-col">
          <div className="flex items-center gap-1.5 text-xs font-medium text-slate-400">
            <span>{meta.category}</span>
            <span>/</span>
            <span className="text-slate-600">{meta.title}</span>
          </div>
          <h1 className="text-lg font-bold tracking-tight text-slate-900">{meta.title}</h1>
        </div>
      </div>

      {/* Right Controls */}
      <div className="flex items-center gap-4">
        {/* Search Input Visual */}
        <div className="hidden items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs text-slate-400 md:flex">
          <Search className="h-3.5 w-3.5 text-slate-400" />
          <span>Search vouchers, accounts, items...</span>
          <kbd className="rounded border border-slate-300 bg-white px-1.5 py-0.5 font-mono text-[10px] text-slate-500">
            Ctrl+K
          </kbd>
        </div>

        {/* Tenant & Company Pill */}
        <div className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs text-slate-600">
          <Building2 className="h-3.5 w-3.5 text-sky-600" />
          <span className="font-medium text-slate-800">Company:</span>
          <span className="font-mono text-[11px] text-slate-500" title={companyId}>
            {companyId ? `${companyId.substring(0, 8)}...` : 'Dev Co.'}
          </span>
          <span className="text-slate-300">|</span>
          <ShieldCheck className="h-3.5 w-3.5 text-emerald-600" />
          <span className="font-mono text-[11px] text-slate-500" title={tenantId}>
            {tenantId ? `${tenantId.substring(0, 8)}...` : 'Default'}
          </span>
        </div>

        {/* System Online Status */}
        <div className="flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-700">
          <CheckCircle2 className="h-3.5 w-3.5" />
          <span className="hidden sm:inline">Connected</span>
        </div>

        {/* Notifications Icon */}
        <button
          type="button"
          className="relative rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          title="Notifications"
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
