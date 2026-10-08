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
import { HrPayrollOverview } from './features/hr-payroll/HrPayrollOverview'
import { ManufacturingOverview } from './features/manufacturing/ManufacturingOverview'
import { SellingOverview } from './features/selling/SellingOverview'
import { StockOverview } from './features/stock/StockOverview'
import { WarehouseView } from './features/stock/pages/WarehouseView'
import { ItemView } from './features/stock/pages/ItemView'
import { AssetCategoryView } from './features/assets/AssetCategoryView'
import { AssetView } from './features/assets/AssetView'
import { CustomerView } from './features/selling/pages/CustomerView'
import { SupplierView } from './features/buying/pages/SupplierView'
import { BankAccountsView } from './features/banking/pages/BankAccountsView'
import { useNavigationStore } from './store/useNavigationStore'
import { useTenantStore } from './store/useTenantStore'

function App() {
  const { t } = useTranslation('accounting')
  const companyId = useTenantStore((state) => state.companyId)
  const currentRoute = useNavigationStore((state) => state.currentRoute)

  return (
    <AppShell>
      {currentRoute === 'dashboard' && <DashboardOverview />}

      {currentRoute === 'accounting-coa' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold tracking-tight text-slate-900">{t('page.title')}</h2>
              <p className="text-xs text-slate-500">{t('page.subtitle')}</p>
            </div>
          </div>
          <AccountTreeTable companyId={companyId} />
        </div>
      )}

      {currentRoute === 'accounting-journal' && <GeneralLedgerOverview />}

      {currentRoute === 'accounting-currencies' && <CurrenciesView />}

      {currentRoute === 'accounting-period-closing' && <FiscalClosingView />}

      {currentRoute === 'accounting-exchange-rates' && <ExchangeRateList />}

      {currentRoute === 'accounting-fx-revaluations' && <ExchangeRateRevaluationsView />}

      {currentRoute === 'assets-categories' && <AssetCategoryView />}
      
      {currentRoute === 'assets' && <AssetView />}

      {currentRoute === 'banking' && <BankingOverview />}

      {currentRoute === 'banking-accounts' && <BankAccountsView />}

      {currentRoute === 'stock' && <StockOverview />}

      {currentRoute === 'stock-warehouses' && <WarehouseView />}

      {currentRoute === 'stock-items' && <ItemView />}

      {currentRoute === 'manufacturing' && <ManufacturingOverview />}

      {currentRoute === 'selling' && <SellingOverview />}

      {currentRoute === 'selling-customers' && <CustomerView />}

      {currentRoute === 'buying' && <BuyingOverview />}

      {currentRoute === 'buying-suppliers' && <SupplierView />}

      {currentRoute === 'hr-payroll' && <HrPayrollOverview />}

      {currentRoute === 'crm' && <CrmOverview />}
    </AppShell>
  )
}

export default App
