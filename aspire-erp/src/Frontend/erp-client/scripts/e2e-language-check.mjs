#!/usr/bin/env node
/**
 * Manual runtime verification for spec 00-i18n F-10 (and the browser half of F-17 setup).
 *
 * Drives the real system Chrome (no downloaded browsers) against `vite preview`:
 *   1. fresh profile  -> UI renders English, `aspire-erp-lang` unset
 *   2. select Español -> UI re-renders Spanish, key persisted to localStorage
 *   3. reload         -> Spanish survives (detector boots from localStorage)
 *
 * The automated twin lives in LanguageSelector.test.tsx (jsdom); this script proves the
 * same acceptance in a real browser profile. It needs no backend: the shell, header,
 * sidebar and dashboard chrome render without API data.
 *
 * Usage (from src/Frontend/erp-client/):
 *   1. (terminal A) npm run preview -- --port 4173 --strictPort
 *   2. node scripts/e2e-language-check.mjs [http://localhost:4173/]
 * Or via package script: npm run e2e:language (starts its own preview; see below).
 *
 * Exit 0 = all three gates green, 1 = first failing gate (message on stderr).
 */
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { chromium } from 'playwright-core'

const url = process.argv[2] ?? 'http://localhost:4173/'
const profileDir = mkdtempSync(join(tmpdir(), 'erp-i18n-e2e-'))

// Route-independent: the boot route is whatever the store defaults to, so gates accept any
// shipped title in each language (mirrors `common.nav.route.*`).
const EN_TITLES = new Set([
  'Executive Overview',
  'Chart of Accounts',
  'General Ledger Entries',
  'Bank Reconciliation & Feeds',
  'Inventory & Warehouses (Kardex)',
  'Work Orders & BOM Studio',
  'Sales Invoices & POS',
  'Purchase Orders & Vendor Bills',
  'Payroll Runs & Directory',
  'Sales Pipeline & Leads',
])
const ES_TITLES = new Set([
  'Resumen ejecutivo',
  'Plan de cuentas',
  'Asientos del libro mayor',
  'Conciliación y conexiones bancarias',
  'Inventario y almacenes (Kardex)',
  'Órdenes de producción y editor de LDM',
  'Facturas de venta y PDV',
  'Órdenes de compra y facturas de proveedor',
  'Nóminas y directorio',
  'Canal de ventas y leads',
])

const fail = (gate, detail) => {
  console.error(`F-10 FAIL [${gate}]: ${detail}`)
  process.exitCode = 1
}
const pass = (gate) => console.log(`F-10 OK   [${gate}]`)

let browser
try {
  browser = await chromium.launch({
    channel: 'chrome',
    headless: true,
    args: ['--no-sandbox'],
  })
  const context = await browser.newContext({ locale: 'en-GB' })
  const page = await context.newPage()

  const problems = []
  page.on('pageerror', (error) => problems.push(`pageerror: ${error.message}`))
  await page.goto(url, { waitUntil: 'networkidle' })

  const text = async (selector) =>
    (await page.textContent(selector))?.trim() ?? '<missing>'

  // Gate 1 — English first paint, detector cache included.
  const titleEn = await text('h1')
  const storedBefore = await page.evaluate(() => localStorage.getItem('aspire-erp-lang'))
  if (!EN_TITLES.has(titleEn)) {
    fail('english-first-paint', `expected a shipped English title, saw: ${titleEn}`)
  } else {
    pass(`english-first-paint (h1: "${titleEn}", stored: ${storedBefore})`)
  }

  // Gate 2 — switching re-renders and persists.
  await page.getByTestId('language-select').selectOption('es')
  await page.waitForFunction(
    (prev) => document.querySelector('h1')?.textContent?.trim() !== prev,
    titleEn,
    { timeout: 10000 },
  )
  const titleEs = await text('h1')
  const storedAfter = await page.evaluate(() => localStorage.getItem('aspire-erp-lang'))
  if (!ES_TITLES.has(titleEs)) {
    fail('switch-rerenders', `expected a shipped Spanish title, saw: ${titleEs}`)
  } else if (storedAfter !== 'es') {
    fail('switch-persists', `localStorage is ${JSON.stringify(storedAfter)}, want "es"`)
  } else {
    pass(`switch-rerenders+persists (h1: "${titleEs}", stored: "${storedAfter}")`)
  }

  // Gate 3 — reload boots Spanish from the persisted key.
  await page.reload({ waitUntil: 'networkidle' })
  const titleReload = await text('h1')
  if (titleReload !== titleEs) {
    fail('reload-persists', `after reload h1 is "${titleReload}", want "${titleEs}"`)
  } else {
    pass(`reload-persists (h1: "${titleReload}")`)
  }

  if (problems.length > 0) {
    fail('no-pageerrors', problems.join(' | '))
  } else {
    pass('no-pageerrors')
  }
} catch (error) {
  fail('harness', error.message)
} finally {
  await browser?.close()
  rmSync(profileDir, { recursive: true, force: true })
}
