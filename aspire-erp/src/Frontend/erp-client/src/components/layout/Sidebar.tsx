import {
  BookOpenCheck,
  Boxes,
  ChevronLeft,
  ChevronRight,
  Factory,
  Handshake,
  Landmark,
  Layers,
  LayoutDashboard,
  ListTree,
  Receipt,
  Truck,
  Users,
} from 'lucide-react'
import type { ElementType } from 'react'
import { type NavRoute, useNavigationStore } from '../../store/useNavigationStore'

interface NavItem {
  id: NavRoute
  label: string
  icon: ElementType
  badge?: string
}

interface NavGroup {
  title: string
  items: NavItem[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    title: 'Overview',
    items: [
      { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
    ],
  },
  {
    title: 'Accounting & Finance',
    items: [
      { id: 'accounting-coa', label: 'Chart of Accounts', icon: ListTree },
      { id: 'accounting-journal', label: 'General Ledger', icon: BookOpenCheck },
      { id: 'banking', label: 'Banking & Rec', icon: Landmark, badge: 'ERPNext' },
    ],
  },
  {
    title: 'Operations',
    items: [
      { id: 'stock', label: 'Stock & Kardex', icon: Boxes },
      { id: 'manufacturing', label: 'Manufacturing', icon: Factory, badge: 'ERPNext' },
      { id: 'selling', label: 'Selling & POS', icon: Receipt },
      { id: 'buying', label: 'Buying & Vendors', icon: Truck },
      { id: 'hr-payroll', label: 'HR & Payroll', icon: Users, badge: 'ERPNext' },
      { id: 'crm', label: 'CRM & Pipeline', icon: Handshake, badge: 'ERPNext' },
    ],
  },
]

export function Sidebar() {
  const currentRoute = useNavigationStore((state) => state.currentRoute)
  const setCurrentRoute = useNavigationStore((state) => state.setCurrentRoute)
  const isSidebarCollapsed = useNavigationStore((state) => state.isSidebarCollapsed)
  const toggleSidebar = useNavigationStore((state) => state.toggleSidebar)

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
          title={isSidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        >
          {isSidebarCollapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
        </button>
      </div>

      {/* Nav List */}
      <nav className="flex-1 space-y-6 overflow-y-auto px-3 py-4">
        {NAV_GROUPS.map((group) => (
          <div key={group.title} className="space-y-1">
            {!isSidebarCollapsed && (
              <h2 className="px-3 text-[11px] font-semibold tracking-wider text-slate-400 uppercase">
                {group.title}
              </h2>
            )}
            <ul className="space-y-1">
              {group.items.map((item) => {
                const Icon = item.icon
                const isActive = currentRoute === item.id

                return (
                  <li key={item.id}>
                    <button
                      type="button"
                      onClick={() => setCurrentRoute(item.id)}
                      title={isSidebarCollapsed ? item.label : undefined}
                      className={`group flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                        isActive
                          ? 'bg-sky-50 text-sky-700 shadow-xs'
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
                          <span className="truncate">{item.label}</span>
                          {item.badge && (
                            <span className="rounded bg-sky-100 px-1.5 py-0.5 text-[10px] font-semibold text-sky-700">
                              {item.badge}
                            </span>
                          )}
                        </div>
                      )}
                    </button>
                  </li>
                )
              })}
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
