import i18n from '../lib/i18n'

/**
 * Test fixtures for the i18n suite (spec 00-i18n F-20…F-22).
 *
 * The production singleton (`src/lib/i18n.ts`) loads namespaces over HTTP, which does not
 * exist under Vitest — so every suite uses a no-op backend (mocked per test file, hoisted
 * above the singleton import) and registers minimal inline bundles here. The SHAPE mirrors
 * production (`common.header.language`, `error.<code>`); key parity with the real catalogs
 * is enforced elsewhere (`i18n-sync --check` + `tsc`), so fixtures stay small on purpose.
 */
export async function loadTestFixtures(): Promise<void> {
  i18n.addResourceBundle(
    'en',
    'common',
    { header: { language: 'Language' } },
    true,
    true,
  )
  i18n.addResourceBundle(
    'es',
    'common',
    { header: { language: 'Idioma' } },
    true,
    true,
  )
  i18n.addResourceBundle(
    'en',
    'error',
    { crm_company_required: 'A company is required.' },
    true,
    true,
  )
  i18n.addResourceBundle(
    'es',
    'error',
    { crm_company_required: 'Se requiere una empresa.' },
    true,
    true,
  )
  await i18n.changeLanguage('en')
}

export { i18n }
