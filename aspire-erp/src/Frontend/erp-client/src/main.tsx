import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@radix-ui/themes/styles.css'
import './index.css'
import './lib/i18n'
import { Theme } from '@radix-ui/themes'
import App from './App.tsx'
import { resolveTenantFromSubdomain } from './store/useTenantStore'

// Host-based tenant resolution happens before the first paint when the app runs on a tenant
// subdomain. It also applies the workspace default language unless the user manually picked one.
// Localhost has no subdomain, so the env/persisted context is used unchanged.
async function bootstrap() {
  await resolveTenantFromSubdomain()

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <Theme accentColor="yellow" grayColor="sand">
        <App />
      </Theme>
    </StrictMode>,
  )
}

void bootstrap()
