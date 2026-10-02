import { create } from 'zustand'
import { persist } from 'zustand/middleware'

/**
 * Tenant + company context for the session (Constitution Article II.1: every request is
 * tenant-scoped, so the SPA always knows which tenant/company it is talking to).
 *
 * Resolution order is env-first: `VITE_TENANT_ID` / `VITE_COMPANY_ID` (`.env.development`
 * ships the dev-seed GUIDs from `scripts/seed-dev-coa.sql`) beat the persisted value on
 * every rehydration, so a stale local session can never point the app at another tenant.
 */
interface TenantContext {
  tenantId: string
  companyId: string
  setTenantId: (tenantId: string) => void
  setCompanyId: (companyId: string) => void
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
      setTenantId: (tenantId) => set({ tenantId }),
      setCompanyId: (companyId) => set({ companyId }),
    }),
    {
      name: 'aspire-erp-tenant',
      // localStorage lands after the initial render, so re-apply env precedence here too.
      merge: (persisted, current) => {
        const stored = (persisted ?? {}) as Partial<TenantContext>
        return {
          ...current,
          ...stored,
          tenantId: isGuid(envTenantId) ? envTenantId : (stored.tenantId ?? current.tenantId),
          companyId: isGuid(envCompanyId) ? envCompanyId : (stored.companyId ?? current.companyId),
        }
      },
    },
  ),
)

/** Current tenant id (env wins over the persisted session). */
export const getTenantId = (): string => useTenantStore.getState().tenantId

/** Current company id (env wins over the persisted session). */
export const getCompanyId = (): string => useTenantStore.getState().companyId
