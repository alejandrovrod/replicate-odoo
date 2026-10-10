import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface User {
  fullName: string
  email: string
  perms: string[]
  roles: string[]
}

interface AuthState {
  token: string | null
  user: User | null
  login: (token: string, fullName: string, email: string) => void
  logout: () => void
  hasPermission: (resource: string, action?: string) => boolean
  isSystemManager: () => boolean
}

/**
 * Basic JWT payload parser. Extracts claims without verifying the signature.
 * (Verification always happens backend-side).
 */
function parseJwt(token: string): Record<string, unknown> | null {
  try {
    const base64Url = token.split('.')[1]
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
    const jsonPayload = decodeURIComponent(
      window
        .atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    )
    return JSON.parse(jsonPayload) as Record<string, unknown>
  } catch {
    return null
  }
}

function parseStringArray(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.filter((v): v is string => typeof v === 'string')
  }
  if (typeof value === 'string' && value !== '') {
    return [value]
  }
  return []
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      user: null,

      login: (token: string, fullName: string, email: string) => {
        const payload = parseJwt(token)

        set({
          token,
          user: {
            fullName,
            email,
            perms: parseStringArray(payload?.perms),
            roles: parseStringArray(payload?.roles),
          },
        })
      },

      logout: () => set({ token: null, user: null }),

      hasPermission: (resource: string, action = 'read') => {
        const { user } = get()
        if (!user) return false

        // System Manager bypass mirrors DocTypePermissionAuthorizationHandler.
        if (user.roles?.some((r) => r.toLowerCase() === 'system manager')) return true

        return user.perms?.includes(`${resource}:${action}`) || user.perms?.includes('all') || false
      },

      isSystemManager: () => {
        const { user } = get()
        return user?.roles?.some((r) => r.toLowerCase() === 'system manager') ?? false
      },
    }),
    {
      name: 'aspire-erp-auth',
    }
  )
)

export const getAuthToken = (): string | null => useAuthStore.getState().token
