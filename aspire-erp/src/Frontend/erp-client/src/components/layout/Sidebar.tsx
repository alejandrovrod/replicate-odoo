import {
  BookOpenCheck,
  Boxes,
  ChevronLeft,
  ChevronRight,
  ChevronDown,
  Factory,
  Handshake,
  Landmark,
  Layers,
  LayoutDashboard,
  ListTree,
  Archive,
  Receipt,
  Truck,
  Users,
  Folder,
} from 'lucide-react'
import { type ElementType, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { type NavRoute, useNavigationStore } from '../../store/useNavigationStore'

interface NavItem {
  id: string
  route?: NavRoute
  label: string
  icon: ElementType
  badge?: string
  children?: NavItem[]
}

interface NavGroup {
  id: string
  title: string
  items: NavItem[]
}

/**
 * App navigation (spec 00-i18n: the sidebar is daily-use chrome, so labels and group titles
 * are keys, not strings). Route ids stay the contract — only the rendered text translates.
 * Brand strings ("Aspire ERP", "ERPNext" badges, stack footers) are product identity and stay
 * literal, same as "Dev Co." in the header.
 */
const NAV_GROUPS: NavGroup[] = [
  {
    id: 'overview',
    title: 'nav.group.overview',
    items: [
      { id: 'dashboard', route: 'dashboard', label: 'nav.item.dashboard', icon: LayoutDashboard },
      { id: 'dashboard-reports', route: 'dashboard-reports', label: 'nav.item.dashboardReports', icon: BookOpenCheck },
    ],
  },
  {
    id: 'accounting',
    title: 'nav.group.accounting',
    items: [
      {
        id: 'acc-masters',
        label: 'nav.folder.masters',
        icon: Folder,
        children: [
          { id: 'accounting-coa', route: 'accounting-coa', label: 'nav.item.accountingCoa', icon: ListTree },
          { id: 'accounting-currencies', route: 'accounting-currencies', label: 'nav.item.accountingCurrencies', icon: Receipt },
          { id: 'accounting-exchange-rates', route: 'accounting-exchange-rates', label: 'nav.item.accountingExchangeRates', icon: Receipt },
        ],
      },
      {
        id: 'acc-transactions',
        label: 'nav.folder.transactions',
        icon: BookOpenCheck,
        children: [
          { id: 'accounting-journal', route: 'accounting-journal', label: 'nav.item.accountingJournal', icon: BookOpenCheck },
        ],
      },
      {
        id: 'acc-assets',
        label: 'nav.folder.assets',
        icon: Archive,
        children: [
          { id: 'assets', route: 'assets', label: 'nav.item.assets', icon: Archive },
          { id: 'assets-categories', route: 'assets-categories', label: 'nav.item.assetsCategories', icon: Layers },
        ],
      },
      {
        id: 'acc-banking',
        label: 'nav.folder.banking',
        icon: Landmark,
        children: [
          { id: 'banking', route: 'banking', label: 'nav.item.banking', icon: Landmark, badge: 'ERPNext' },
          { id: 'banking-accounts', route: 'banking-accounts', label: 'nav.item.bankingAccounts', icon: Landmark },
          { id: 'banking-payments', route: 'banking-payments', label: 'nav.item.bankingPayments', icon: Receipt },
        ],
      },
      {
        id: 'acc-settings',
        label: 'nav.folder.config',
        icon: Folder,
        children: [
          { id: 'accounting-settings', route: 'accounting-settings', label: 'nav.item.accountingSettings', icon: Folder },
          { id: 'accounting-period-closing', route: 'accounting-period-closing', label: 'nav.item.periodClosing', icon: Archive },
          { id: 'accounting-fx-revaluations', route: 'accounting-fx-revaluations', label: 'nav.item.accountingFxRevaluations', icon: Archive },
        ],
      },
    ],
  },
  {
    id: 'operations',
    title: 'nav.group.operations',
    items: [
      { id: 'stock', route: 'stock', label: 'nav.item.stock', icon: Boxes },
      { id: 'manufacturing', route: 'manufacturing', label: 'nav.item.manufacturing', icon: Factory, badge: 'ERPNext' },
      {
        id: 'selling-folder',
        label: 'nav.item.selling',
        icon: Receipt,
        children: [
          { id: 'selling', route: 'selling', label: 'nav.item.sellingOverview', icon: Receipt },
          { id: 'selling-customers', route: 'selling-customers', label: 'nav.item.sellingCustomers', icon: Users },
          { id: 'selling-orders', route: 'selling-orders', label: 'nav.item.sellingOrders', icon: BookOpenCheck },
          { id: 'selling-invoices', route: 'selling-invoices', label: 'nav.item.sellingInvoices', icon: Receipt },
          { id: 'selling-deliveries', route: 'selling-deliveries', label: 'nav.item.sellingDeliveries', icon: Truck },
        ],
      },
      { id: 'buying', route: 'buying', label: 'nav.item.buying', icon: Truck },
      { id: 'hr-payroll', route: 'hr-payroll', label: 'nav.item.hrPayroll', icon: Users, badge: 'ERPNext' },
      { id: 'crm', route: 'crm', label: 'nav.item.crm', icon: Handshake, badge: 'ERPNext' },
    ],
  },
]

export function Sidebar() {
  const { t } = useTranslation('common')
  const currentRoute = useNavigationStore((state) => state.currentRoute)
  const setCurrentRoute = useNavigationStore((state) => state.setCurrentRoute)
  const isSidebarCollapsed = useNavigationStore((state) => state.isSidebarCollapsed)
  const toggleSidebar = useNavigationStore((state) => state.toggleSidebar)

  const [expandedFolders, setExpandedFolders] = useState<Record<string, boolean>>({
    'acc-masters': true,
    'acc-transactions': true,
    'acc-assets': true,
    'acc-banking': true,
  })

  const toggleFolder = (id: string) => {
    setExpandedFolders((prev) => ({ ...prev, [id]: !prev[id] }))
  }

  const renderNavItem = (item: NavItem, depth = 0) => {
    const Icon = item.icon
    const isActive = item.route ? currentRoute === item.route : false
    const hasChildren = item.children && item.children.length > 0
    const isExpanded = expandedFolders[item.id]

    // Determine padding based on depth
    // depth 0: px-3 (12px)
    // depth 1: pl-8 pr-3 (32px left padding)
    const paddingLeftClass = depth > 0 && !isSidebarCollapsed ? 'pl-8 pr-3' : 'px-3'

    return (
      <li key={item.id} className="space-y-0.5 block">
        <button
          type="button"
          onClick={() => {
            if (hasChildren) {
              if (!isSidebarCollapsed) toggleFolder(item.id)
            } else if (item.route) {
              setCurrentRoute(item.route)
            }
          }}
          title={isSidebarCollapsed ? t(item.label as any) : undefined}
          className={`group flex w-full items-center gap-3 rounded-md py-2 text-sm font-medium transition-colors ${paddingLeftClass} ${
            isActive
              ? 'bg-sky-50 text-sky-700 shadow-xs'
              : hasChildren
                ? 'text-slate-700 hover:bg-slate-100'
                : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
          }`}
        >
          <Icon
            className={`h-5 w-5 shrink-0 transition-colors ${
              isActive ? 'text-sky-600' : 'text-slate-400 group-hover:text-slate-600'
            }`}
          />
          {!isSidebarCollapsed && (
            <div className="flex flex-1 items-center justify-between overflow-hidden">
              <span className="truncate">{t(item.label as any)}</span>
              <div className="flex items-center gap-2">
                {item.badge && (
                  <span className="rounded bg-sky-100 px-1.5 py-0.5 text-[10px] font-semibold text-sky-700">
                    {item.badge}
                  </span>
                )}
                {hasChildren && (
                  <ChevronDown
                    className={`h-4 w-4 shrink-0 text-slate-400 transition-transform ${
                      isExpanded ? 'rotate-180' : ''
                    }`}
                  />
                )}
              </div>
            </div>
          )}
        </button>
        {hasChildren && !isSidebarCollapsed && isExpanded && (
          <ul className="mt-1 space-y-0.5">
            {item.children!.map((child) => renderNavItem(child, depth + 1))}
          </ul>
        )}
      </li>
    )
  }

  return (
    <aside
      className={`relative flex flex-col border-r border-slate-200 bg-white transition-all duration-300 ease-in-out ${
        isSidebarCollapsed ? 'w-18' : 'w-64'
      }`}
    >
      {/* Brand Header */}
      <div className="flex h-16 items-center justify-between border-b border-slate-200 px-4">
        <div className="flex items-center gap-3 overflow-hidden">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-sky-600 text-white shadow-sm">
            <Layers className="h-6 w-6" />
          </div>
          {!isSidebarCollapsed && (
            <div className="flex flex-col">
              <span className="text-base font-bold tracking-tight text-slate-900">Aspire ERP</span>
              <span className="text-xs font-medium text-sky-600">Enterprise Cloud</span>
            </div>
          )}
        </div>

        <button
          type="button"
          onClick={toggleSidebar}
          className="flex h-7 w-7 items-center justify-center rounded-md border border-slate-200 text-slate-500 hover:bg-slate-100 hover:text-slate-900 focus:outline-none"
          title={t(isSidebarCollapsed ? 'nav.expandSidebar' : 'nav.collapseSidebar')}
        >
          {isSidebarCollapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
        </button>
      </div>

      {/* Nav List */}
      <nav className="flex-1 space-y-6 overflow-y-auto px-3 py-4">
        {NAV_GROUPS.map((group) => (
          <div key={group.id} className="space-y-1">
            {!isSidebarCollapsed && (
              <h2 className="px-3 text-[11px] font-semibold tracking-wider text-slate-400 uppercase">
                {t(group.title as any)}
              </h2>
            )}
            <ul className="space-y-1">
              {group.items.map((item) => renderNavItem(item, 0))}
            </ul>
          </div>
        ))}
      </nav>

      {/* Sidebar Footer */}
      {!isSidebarCollapsed && (
        <div className="border-t border-slate-200 p-4">
          <div className="flex items-center gap-2 rounded-md bg-slate-50 p-2.5 text-xs text-slate-500">
            <span className="inline-block h-2 w-2 rounded-full bg-emerald-500" />
            <div className="flex flex-col">
              <span className="font-medium text-slate-700">.NET Aspire · SQL 2025</span>
              <span className="text-[10px] text-slate-400">DDD & Clean Architecture</span>
            </div>
          </div>
        </div>
      )}
    </aside>
  )
}
