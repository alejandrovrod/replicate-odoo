import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import i18n, {
  DEFAULT_LANGUAGE,
  hasManualLanguagePreference,
  normalizeLanguage,
  type SupportedLanguage,
} from '../lib/i18n'
import { getCurrentSubdomain } from '../lib/subdomain'
import { fetchTenantBySubdomain } from '../features/tenants/tenantApi'

/**
 * Tenant + company context for the session (Constitution Article II.1: every request is
 * tenant-scoped, so the SPA always knows which tenant/company it is talking to).
 *
 * Resolution order:
 * 1. A host subdomain (`acme.domain.com` / `acme.localhost`) resolved at boot.
 * 2. `VITE_TENANT_ID` / `VITE_COMPANY_ID` (the dev seed from `scripts/seed-dev-coa.sql`).
 * 3. The persisted session.
 *
 * Host resolution therefore wins over the env pin: a real tenant workspace can never show
 * another tenant's data. Local dev on `localhost` has no subdomain, so the env values apply.
 */
interface TenantContext {
  tenantId: string
  companyId: string
  tenantName: string
  subdomain: string
  defaultLanguage: SupportedLanguage
  setTenantId: (tenantId: string) => void
  setCompanyId: (companyId: string) => void
  setTenantMetadata: (metadata: {
    id: string
    name: string
    subdomain: string
    defaultLanguage?: string
  }) => void
}

const isGuid = (value: unknown): value is string =>
  typeof value === 'string' &&
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)

const envTenantId = import.meta.env.VITE_TENANT_ID as string | undefined
const envCompanyId = import.meta.env.VITE_COMPANY_ID as string | undefined

export const useTenantStore = create<TenantContext>()(
  persist(
    (set) => ({
      tenantId: isGuid(envTenantId) ? envTenantId : '',
      companyId: isGuid(envCompanyId) ? envCompanyId : '',
      tenantName: '',
      subdomain: '',
      defaultLanguage: DEFAULT_LANGUAGE,
      setTenantId: (tenantId) => set({ tenantId }),
      setCompanyId: (companyId) => set({ companyId }),
      setTenantMetadata: (metadata) =>
        set({
          tenantId: metadata.id,
          tenantName: metadata.name,
          subdomain: metadata.subdomain,
          defaultLanguage: normalizeLanguage(metadata.defaultLanguage),
        }),
    }),
    {
      name: 'aspire-erp-tenant',
      // localStorage lands after the initial render, so re-apply env precedence here too.
      // Metadata is preserved independently: host resolution overwrites it right after boot.
      merge: (persisted, current) => {
        const stored = (persisted ?? {}) as Partial<TenantContext>
        return {
          ...current,
          ...stored,
          tenantId: isGuid(envTenantId) ? envTenantId : (stored.tenantId ?? current.tenantId),
          companyId: isGuid(envCompanyId) ? envCompanyId : (stored.companyId ?? current.companyId),
          tenantName: stored.tenantName ?? current.tenantName,
          subdomain: stored.subdomain ?? current.subdomain,
          defaultLanguage: normalizeLanguage(
            stored.defaultLanguage ?? current.defaultLanguage,
          ),
        }
      },
    },
  ),
)

/** Current tenant id (env wins over the persisted session). */
export const getTenantId = (): string => useTenantStore.getState().tenantId

/** Current company id (env wins over the persisted session). */
export const getCompanyId = (): string => useTenantStore.getState().companyId

/**
 * Boot-time host resolution. When the SPA is served from `{subdomain}.domain.com` (or
 * `{subdomain}.localhost`), fetch the tenant metadata and pin the store before the shell
 * renders. Failures are non-fatal: `localhost` dev and the login tenant picker keep working.
 */
export async function resolveTenantFromSubdomain(): Promise<boolean> {
  const subdomain = getCurrentSubdomain()
  if (!subdomain) return false

  try {
    const tenant = await fetchTenantBySubdomain(subdomain)
    useTenantStore.getState().setTenantMetadata(tenant)

    // The workspace default wins unless the user explicitly picked a language on this browser.
    // i18next's detector cache is not a manual preference: it also caches automatically.
    if (!hasManualLanguagePreference()) {
      await i18n.changeLanguage(normalizeLanguage(tenant.defaultLanguage))
    }

    return true
  } catch {
    return false
  }
}
