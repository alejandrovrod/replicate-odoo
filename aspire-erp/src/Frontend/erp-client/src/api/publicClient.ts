import axios from 'axios'

/**
 * HTTP client for public endpoints that must work BEFORE a tenant is resolved
 * (`/api/v1/tenants/*`). It deliberately does not import the tenant/auth stores: the boot
 * resolver writes to the tenant store, so importing `api/client.ts` here would create a cycle.
 */
export const publicClient = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})
