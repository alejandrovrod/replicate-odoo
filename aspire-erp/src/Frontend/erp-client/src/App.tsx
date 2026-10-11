import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { AppShell } from './components/layout/AppShell'
import { AccountTreeTable } from './features/accounting/AccountTreeTable'
import { CurrenciesView } from './features/accounting/CurrenciesView'
import { GeneralLedgerOverview } from './features/accounting/GeneralLedgerOverview'
import { FiscalClosingView } from './features/accounting/components/FiscalClosingView'
import { ExchangeRateList } from './features/accounting/components/ExchangeRateList'
import { ExchangeRateRevaluationsView } from './features/accounting/components/ExchangeRateRevaluationsView'
import { BankingOverview } from './features/banking/BankingOverview'
import { BuyingOverview } from './features/buying/BuyingOverview'
import { CrmOverview } from './features/crm/CrmOverview'
import { DashboardOverview } from './features/dashboard/DashboardOverview'
import { ReportsView } from './features/dashboard/pages/ReportsView'
import { HrPayrollOverview } from './features/hr-payroll/HrPayrollOverview'
import { ManufacturingOverview } from './features/manufacturing/ManufacturingOverview'
import { SellingOverview } from './features/selling/SellingOverview'
import { StockOverview } from './features/stock/StockOverview'
import { WarehouseView } from './features/stock/pages/WarehouseView'
import { ItemView } from './features/stock/pages/ItemView'
import { AssetCategoryView } from './features/assets/AssetCategoryView'
import { AssetView } from './features/assets/AssetView'
import { CustomerView } from './features/selling/pages/CustomerView'
import { DeliveryNotesView } from './features/selling/pages/DeliveryNotesView'
import { SalesInvoicesView } from './features/selling/pages/SalesInvoicesView'
import { SalesOrdersView } from './features/selling/pages/SalesOrdersView'
import { SupplierView } from './features/buying/pages/SupplierView'
import { BankAccountsView } from './features/banking/pages/BankAccountsView'
import { PaymentsView } from './features/banking/pages/PaymentsView'
import { useNavigationStore, type NavRoute } from './store/useNavigationStore'
import { useTenantStore } from './store/useTenantStore'
import { AccountingSettingsView } from './features/accounting/AccountingSettingsView'
import { ProfileScreen } from './features/profile/ProfileScreen'
import { LoginScreen } from './components/auth/LoginScreen'
import { RequirePermission } from './components/auth/RequirePermission'
import { useAuthStore } from './store/useAuthStore'

/**
 * Route-level DocType gates mirroring the API `permission:doctype:read` policies and the
 * sidebar `perm` annotations. Routes ABSENT from this map (dashboard) render for every
 * authenticated user. Direct hash navigation to a gated route renders the forbidden card
 * instead of a data-less view (the API still enforces the real policy).
 */
const ROUTE_PERMS: Partial<Record<NavRoute, [string, string?]>> = {
  'dashboard-reports': ['report', 'read'],
  'accounting-coa': ['account', 'read'],
  'accounting-journal': ['journal_entry', 'read'],
  'accounting-currencies': ['currency', 'read'],
  'accounting-period-closing': ['period_closing_voucher', 'read'],
  'accounting-exchange-rates': ['exchange_rate', 'read'],
  'accounting-fx-revaluations': ['exchange_rate_revaluation', 'read'],
  'accounting-settings': ['company', 'read'],
  'assets-categories': ['asset_category', 'read'],
  assets: ['asset', 'read'],
  banking: ['bank_transaction', 'read'],
  'banking-accounts': ['bank_account', 'read'],
  'banking-payments': ['payment_entry', 'read'],
  stock: ['stock', 'read'],
  'stock-warehouses': ['warehouse', 'read'],
  'stock-items': ['item', 'read'],
  manufacturing: ['work_order', 'read'],
  selling: ['sales_invoice', 'read'],
  'selling-customers': ['customer', 'read'],
  'selling-orders': ['sales_order', 'read'],
  'selling-invoices': ['sales_invoice', 'read'],
  'selling-deliveries': ['delivery_note', 'read'],
  buying: ['purchase_order', 'read'],
  'buying-suppliers': ['supplier', 'read'],
  'hr-payroll': ['payroll_entry', 'read'],
  crm: ['lead', 'read'],
}

function Forbidden() {
  const { t } = useTranslation('common')
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border border-slate-200 bg-white p-12 text-center shadow-xs">
      <span className="text-4xl font-bold text-slate-300">403</span>
      <h2 className="text-base font-semibold text-slate-900">{t('auth.forbiddenTitle', 'No access')}</h2>
      <p className="max-w-sm text-xs text-slate-500">
        {t('auth.forbiddenDetail', 'Your role does not include this module. Contact an administrator.')}
      </p>
    </div>
  )
}

function App() {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore((state) => state.companyId)
  const currentRoute = useNavigationStore((state) => state.currentRoute)

  const { token } = useAuthStore()

  if (!token) {
    return <LoginScreen />
  }

  const guard = (route: NavRoute, view: ReactNode) => {
    if (currentRoute !== route) return null
    const perm = ROUTE_PERMS[route]
    if (!perm) return <>{view}</>
    return (
      <RequirePermission resource={perm[0]} action={perm[1] ?? 'read'} fallback={<Forbidden />}>
        {view}
      </RequirePermission>
    )
  }

  return (
    <AppShell>
      {guard('dashboard', <DashboardOverview />)}
      {/* Profile is intentionally ungated: every authenticated user owns their password + MFA. */}
      {currentRoute === 'profile' ? <ProfileScreen /> : null}
      {guard('dashboard-reports', <ReportsView />)}
      {guard('accounting-coa', (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold tracking-tight text-slate-900">{t('page.title')}</h2>
              <p className="text-xs text-slate-500">{t('page.subtitle')}</p>
            </div>
          </div>
          <AccountTreeTable companyId={companyId} />
        </div>
      ))}
      {guard('accounting-journal', <GeneralLedgerOverview />)}
      {guard('accounting-currencies', <CurrenciesView />)}
      {guard('accounting-period-closing', <FiscalClosingView />)}
      {guard('accounting-exchange-rates', <ExchangeRateList />)}
      {guard('accounting-fx-revaluations', <ExchangeRateRevaluationsView />)}
      {guard('accounting-settings', <AccountingSettingsView />)}
      {guard('assets-categories', <AssetCategoryView />)}
      {guard('assets', <AssetView />)}
      {guard('banking', <BankingOverview />)}
      {guard('banking-accounts', <BankAccountsView />)}
      {guard('banking-payments', <PaymentsView />)}
      {guard('stock', <StockOverview />)}
      {guard('stock-warehouses', <WarehouseView />)}
      {guard('stock-items', <ItemView />)}
      {guard('manufacturing', <ManufacturingOverview />)}
      {guard('selling', <SellingOverview />)}
      {guard('selling-customers', <CustomerView />)}
      {guard('selling-orders', <SalesOrdersView />)}
      {guard('selling-invoices', <SalesInvoicesView />)}
      {guard('selling-deliveries', <DeliveryNotesView />)}
      {guard('buying', <BuyingOverview />)}
      {guard('buying-suppliers', <SupplierView />)}
      {guard('hr-payroll', <HrPayrollOverview />)}
      {guard('crm', <CrmOverview />)}
    </AppShell>
  )
}

export default App
