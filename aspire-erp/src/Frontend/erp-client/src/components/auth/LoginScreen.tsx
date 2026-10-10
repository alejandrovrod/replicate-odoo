import React, { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Building2, Lock, Mail, Loader2 } from 'lucide-react'
import { apiClient, ApiError } from '../../api/client'
import { useAuthStore } from '../../store/useAuthStore'
import { useTenantStore } from '../../store/useTenantStore'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'

// Env-pinned tenants (dev .env) win over everything: no override field is shown then.
const ENV_TENANT_PINNED = Boolean(import.meta.env.VITE_TENANT_ID)

export function LoginScreen() {
  const { t } = useTranslation('common')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [tenantOverride, setTenantOverride] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const login = useAuthStore((state) => state.login)
  const setTenantId = useTenantStore((state) => state.setTenantId)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError('')

    try {
      // The tenant travels in the X-Tenant-ID header (injected by apiClient);
      // the backend resolves it via middleware, not from this body.
      if (!ENV_TENANT_PINNED && tenantOverride.trim() !== '') {
        setTenantId(tenantOverride.trim())
      }
      const response = await apiClient.post('/v1/auth/login', { email, password })

      const { token, fullName, email: userEmail } = response.data
      login(token, fullName, userEmail)
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(err.message || t('auth.errorGeneric', 'Sign-in failed. Check your credentials.'))
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError(t('auth.errorGeneric', 'Sign-in failed. Check your credentials.'))
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 sm:px-6 lg:px-8">
      <div className="w-full max-w-md rounded-xl border border-slate-200 bg-white shadow-lg overflow-hidden">
        <div className="flex flex-col space-y-1.5 p-6 text-center">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-blue-100">
            <Building2 className="h-6 w-6 text-blue-600" />
          </div>
          <h3 className="text-2xl font-semibold leading-none tracking-tight">Aspire ERP</h3>
          <p className="text-sm text-slate-500">{t('auth.subtitle', 'Sign in to your tenant workspace')}</p>
        </div>
        <div className="p-6 pt-0">
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && <div className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</div>}

            <div className="space-y-2">
              <label
                htmlFor="email"
                className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
              >
                {t('auth.email', 'Email')}
              </label>
              <div className="relative">
                <Mail className="absolute left-3 top-3 h-4 w-4 text-slate-400" />
                <Input
                  id="email"
                  type="email"
                  required
                  autoComplete="username"
                  className="pl-9"
                  placeholder={t('auth.emailPlaceholder', 'you@company.com')}
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>
            </div>

            <div className="space-y-2">
              <label
                htmlFor="password"
                className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
              >
                {t('auth.password', 'Password')}
              </label>
              <div className="relative">
                <Lock className="absolute left-3 top-3 h-4 w-4 text-slate-400" />
                <Input
                  id="password"
                  type="password"
                  required
                  autoComplete="current-password"
                  className="pl-9"
                  placeholder="••••••••"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>
            </div>

            {!ENV_TENANT_PINNED && (
              <div className="space-y-2">
                <label
                  htmlFor="tenant"
                  className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                >
                  {t('auth.tenant', 'Tenant ID')}
                </label>
                <Input
                  id="tenant"
                  value={tenantOverride}
                  onChange={(e) => setTenantOverride(e.target.value)}
                  placeholder={t('auth.tenantPlaceholder', 'Tenant GUID for this workspace')}
                />
              </div>
            )}

            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  {t('auth.signingIn', 'Signing in...')}
                </>
              ) : (
                t('auth.signIn', 'Sign In')
              )}
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
