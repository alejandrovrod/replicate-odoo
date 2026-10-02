import { AccountTreeTable } from './features/accounting/AccountTreeTable'
import { useTenantStore } from './store/useTenantStore'

function App() {
  const companyId = useTenantStore((state) => state.companyId)
  const tenantId = useTenantStore((state) => state.tenantId)

  return (
    <main className="min-h-screen bg-slate-50 px-6 py-8 text-slate-900">
      <header className="mb-6 flex flex-col gap-1">
        <h1 className="text-2xl font-bold tracking-tight">Chart of Accounts</h1>
        <p className="text-sm text-slate-500">
          Tenant <span className="font-mono">{tenantId || 'not set'}</span> · Company{' '}
          <span className="font-mono">{companyId || 'not set'}</span>
        </p>
      </header>

      <AccountTreeTable companyId={companyId} />
    </main>
  )
}

export default App
