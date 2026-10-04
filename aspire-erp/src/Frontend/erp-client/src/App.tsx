import { AppShell } from './components/layout/AppShell'
import { AccountTreeTable } from './features/accounting/AccountTreeTable'
import { GeneralLedgerOverview } from './features/accounting/GeneralLedgerOverview'
import { BankingOverview } from './features/banking/BankingOverview'
import { BuyingOverview } from './features/buying/BuyingOverview'
import { DashboardOverview } from './features/dashboard/DashboardOverview'
import { ManufacturingOverview } from './features/manufacturing/ManufacturingOverview'
import { SellingOverview } from './features/selling/SellingOverview'
import { StockOverview } from './features/stock/StockOverview'
import { useNavigationStore } from './store/useNavigationStore'
import { useTenantStore } from './store/useTenantStore'

function App() {
  const companyId = useTenantStore((state) => state.companyId)
  const currentRoute = useNavigationStore((state) => state.currentRoute)

  return (
    <AppShell>
      {currentRoute === 'dashboard' && <DashboardOverview />}

      {currentRoute === 'accounting-coa' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold tracking-tight text-slate-900">Chart of Accounts</h2>
              <p className="text-xs text-slate-500">
                Hierarchical ledger taxonomy (Assets, Liabilities, Equity, Income, Expenses).
              </p>
            </div>
          </div>
          <AccountTreeTable companyId={companyId} />
        </div>
      )}

      {currentRoute === 'accounting-journal' && <GeneralLedgerOverview />}

      {currentRoute === 'banking' && <BankingOverview />}

      {currentRoute === 'stock' && <StockOverview />}

      {currentRoute === 'manufacturing' && <ManufacturingOverview />}

      {currentRoute === 'selling' && <SellingOverview />}

      {currentRoute === 'buying' && <BuyingOverview />}
    </AppShell>
  )
}

export default App
