import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@radix-ui/themes/styles.css'
import './index.css'
import './lib/i18n'
import { Theme } from '@radix-ui/themes'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Theme accentColor="yellow" grayColor="sand">
      <App />
    </Theme>
  </StrictMode>,
)
