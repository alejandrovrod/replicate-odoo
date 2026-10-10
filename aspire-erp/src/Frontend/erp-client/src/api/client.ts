import axios, { type AxiosError } from 'axios'
import { getTenantId } from '../store/useTenantStore'
import { getAuthToken, useAuthStore } from '../store/useAuthStore'
import { LANGUAGE_STORAGE_KEY, normalizeLanguage } from '../lib/i18n'
/**
 * Single entry point for every network call (Constitution Article VII.3).
 *
 * - Injects `X-Tenant-ID` automatically from the tenant store.
 * - Injects `Authorization: Bearer` from the auth store when logged in; a 401 response
 *   clears the session (except on the login call itself) so the app returns to login.
 * - Normalizes RFC 7807 `ProblemDetails` failures into `ApiError`, so callers can branch on
 *   `status` / `code` instead of re-parsing response bodies.
 * - Talks to `/api` only: `vite.config.ts` proxies it to the Aspire API's HTTPS endpoint
 *   (`UseHttpsRedirection` would otherwise 307 every call off-origin), which keeps the SPA
 *   free of hardcoded hosts/ports and avoids CORS entirely.
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  code?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly title?: string

  constructor(status: number, title: string, detail: string, code?: string) {
    super(detail || title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.code = code
  }
}

export const apiClient = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

apiClient.interceptors.request.use((config) => {
  const tenantId = getTenantId()
  if (tenantId) {
    config.headers['X-Tenant-ID'] = tenantId
  }

  const token = getAuthToken()
  if (token) {
    config.headers['Authorization'] = `Bearer ${token}`
  }

  // Spec 00-i18n F-07: the backend localizes ProblemDetails title/detail per Accept-Language.
  // Read the detector's cache directly rather than i18n.language so a request issued before
  // i18next finishes booting still carries the persisted choice.
  let language: string | null = null
  try {
    language = localStorage.getItem(LANGUAGE_STORAGE_KEY)
  } catch {
    // Storage disabled (private mode / hardened browser): navigator detection on the server.
  }
  config.headers['Accept-Language'] = normalizeLanguage(language)

  if ((config.method === 'post' || config.method === 'put') && !config.headers['Idempotency-Key']) {
    config.headers['Idempotency-Key'] = crypto.randomUUID()
  }

  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    const status = error.response?.status ?? 0
    const problem = error.response?.data

    // Expired/revoked token: drop the session so the App gate returns to LoginScreen.
    // The login endpoint itself is excluded (a 401 there just means bad credentials).
    if (status === 401 && !error.config?.url?.includes('/v1/auth/login')) {
      useAuthStore.getState().logout()
    }

    // Request never reached a server (DNS, offline, CORS, aborted): surface it with status 0.
    if (status === 0) {
      return Promise.reject(new ApiError(0, 'Network Error', error.message))
    }

    if (problem && typeof problem === 'object') {
      const extensions = problem as ProblemDetails & Record<string, unknown>
      return Promise.reject(
        new ApiError(
          status,
          problem.title ?? 'Request Failed',
          problem.detail ?? error.message,
          problem.code ?? (typeof extensions.code === 'string' ? extensions.code : undefined),
        ),
      )
    }

    return Promise.reject(
      new ApiError(status, `HTTP ${status}`, error.message || 'Unexpected server response.'),
    )
  },
)
