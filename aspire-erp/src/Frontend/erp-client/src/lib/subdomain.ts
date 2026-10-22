/**
 * Tenant subdomain helpers shared by the boot resolver and the registration form.
 *
 * The backend is the final authority (`TenantSubdomain` in Erp.Domain); this module keeps the
 * UX honest before a request is sent and extracts the tenant label from the current host.
 * Keep the regex and reserved list byte-for-byte aligned with the Domain rule.
 */

export const SUBDOMAIN_PATTERN = /^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$/

export const RESERVED_SUBDOMAINS = [
  'api',
  'app',
  'admin',
  'auth',
  'login',
  'signup',
  'register',
  'www',
  'mail',
  'status',
  'dev',
  'staging',
] as const

/** Deployments override this with `VITE_BASE_DOMAIN` (for example `erp.example.com`). */
export const BASE_DOMAIN =
  (import.meta.env.VITE_BASE_DOMAIN as string | undefined)?.trim().toLowerCase() || 'domain.com'

export function normalizeSubdomain(value: string | null | undefined): string {
  return (value ?? '').trim().toLowerCase()
}

export function isSubdomainFormatValid(value: string | null | undefined): boolean {
  return SUBDOMAIN_PATTERN.test(normalizeSubdomain(value))
}

export function isReservedSubdomain(value: string | null | undefined): boolean {
  return (RESERVED_SUBDOMAINS as readonly string[]).includes(normalizeSubdomain(value))
}

export function isSubdomainValid(value: string | null | undefined): boolean {
  return isSubdomainFormatValid(value) && !isReservedSubdomain(value)
}

/**
 * Extracts the tenant label from a hostname:
 * `acme.domain.com` -> `acme`, `acme.localhost` -> `acme`.
 * Returns null for bare hosts, IPs, nested labels and reserved names.
 */
export function getCurrentSubdomain(
  hostname: string | null | undefined = typeof window === 'undefined' ? '' : window.location.hostname,
  baseDomain: string = BASE_DOMAIN,
): string | null {
  let host = (hostname ?? '').trim().replace(/\.$/, '').toLowerCase()

  const colon = host.lastIndexOf(':')
  if (colon > 0 && host.indexOf(':') === colon) {
    host = host.slice(0, colon)
  }

  if (!host || /^\d{1,3}(\.\d{1,3}){3}$/.test(host) || host.includes(':')) {
    return null
  }

  let candidate: string | null = null

  if (host.endsWith('.localhost')) {
    candidate = host.slice(0, -'.localhost'.length)
  } else {
    const base = normalizeSubdomain(baseDomain) || 'domain.com'
    const suffix = `.${base}`
    if (host.endsWith(suffix)) {
      candidate = host.slice(0, -suffix.length)
    }
  }

  if (!candidate || candidate.includes('.')) return null
  return isSubdomainValid(candidate) ? candidate : null
}

/** Public URL of a tenant workspace (local dev keeps the current port). */
export function tenantUrl(subdomain: string): string {
  const label = normalizeSubdomain(subdomain)

  if (typeof window !== 'undefined') {
    const { hostname, port, protocol } = window.location
    if (hostname === 'localhost' || hostname === '127.0.0.1' || hostname.endsWith('.localhost')) {
      return `${protocol}//${label}.localhost${port ? `:${port}` : ''}`
    }
    return `${protocol}//${label}.${BASE_DOMAIN}`
  }

  return `https://${label}.${BASE_DOMAIN}`
}
