import { create } from 'zustand'

export type NavRoute =
  | 'dashboard'
  | 'dashboard-reports'
  | 'accounting-coa'
  | 'accounting-journal'
  | 'accounting-currencies'
  | 'assets'
  | 'assets-categories'
  | 'banking'
  | 'banking-accounts'
  | 'banking-payments'
  | 'stock'
  | 'manufacturing'
  | 'selling'
  | 'buying'
  | 'hr-payroll'
  | 'crm'
  | 'stock-warehouses'
  | 'stock-items'
  | 'selling-customers'
  | 'selling-orders'
  | 'selling-invoices'
  | 'selling-deliveries'
  | 'buying-suppliers'
  | 'accounting-settings'
  | 'accounting-period-closing'
  | 'accounting-exchange-rates'
  | 'accounting-fx-revaluations'

interface NavigationState {
  currentRoute: NavRoute
  isSidebarCollapsed: boolean
  setCurrentRoute: (route: NavRoute) => void
  toggleSidebar: () => void
  setSidebarCollapsed: (collapsed: boolean) => void
}

const parseHash = (): NavRoute => {
  const hash = window.location.hash.replace(/^#\/?/, '')
  const validRoutes: NavRoute[] = [
    'dashboard',
    'dashboard-reports',
    'accounting-coa',
    'accounting-journal',
    'accounting-currencies',
    'assets',
    'assets-categories',
    'banking',
    'banking-accounts',
    'banking-payments',
    'stock',
    'manufacturing',
    'selling',
    'buying',
    'hr-payroll',
    'crm',
    'stock-warehouses',
    'stock-items',
    'selling-customers',
    'selling-orders',
    'selling-invoices',
    'selling-deliveries',
    'buying-suppliers',
    'accounting-settings',
    'accounting-period-closing',
    'accounting-exchange-rates',
    'accounting-fx-revaluations',
  ]
  return validRoutes.includes(hash as NavRoute) ? (hash as NavRoute) : 'accounting-coa'
}

export const useNavigationStore = create<NavigationState>((set) => {
  // Sync with browser back/forward buttons
  if (typeof window !== 'undefined') {
    window.addEventListener('hashchange', () => {
      set({ currentRoute: parseHash() })
    })
  }

  return {
    currentRoute: typeof window !== 'undefined' ? parseHash() : 'accounting-coa',
    isSidebarCollapsed: false,
    setCurrentRoute: (route: NavRoute) => {
      if (typeof window !== 'undefined') {
        window.location.hash = route
      }
      set({ currentRoute: route })
    },
    toggleSidebar: () => set((state) => ({ isSidebarCollapsed: !state.isSidebarCollapsed })),
    setSidebarCollapsed: (collapsed: boolean) => set({ isSidebarCollapsed: collapsed }),
  }
})
