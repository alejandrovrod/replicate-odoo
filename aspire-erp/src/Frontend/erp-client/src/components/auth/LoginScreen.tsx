import React, { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Building2, Lock, Mail, Loader2, ShieldCheck, ArrowLeft } from 'lucide-react'
import { ApiError } from '../../api/client'
import { loginRequest, verifyLoginMfa } from '../../features/auth/api/useAuth'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { useAuthStore } from '../../store/useAuthStore'
import { useTenantStore } from '../../store/useTenantStore'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'

// Env-pinned tenants (dev .env) win over everything: no override field is shown then.
const ENV_TENANT_PINNED = Boolean(import.meta.env.VITE_TENANT_ID)

type Step = 'password' | 'mfa'

export function LoginScreen() {
  const { t, i18n } = useTranslation('common')
  const [step, setStep] = useState<Step>('password')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [tenantOverride, setTenantOverride] = useState('')
  const [mfaTicket, setMfaTicket] = useState('')
  const [mfaCode, setMfaCode] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const login = useAuthStore((state) => state.login)
  const setTenantId = useTenantStore((state) => state.setTenantId)

  const showError = (err: unknown) => {
    if (err instanceof ApiError) {
      setError(
        translateErrorCode(i18n, err.code, err.message) ||
          t('auth.errorGeneric', 'Sign-in failed. Check your credentials.'),
      )
    } else if (err instanceof Error) {
      setError(err.message)
    } else {
      setError(t('auth.errorGeneric', 'Sign-in failed. Check your credentials.'))
    }
  }

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError('')

    try {
      // The tenant travels in the X-Tenant-ID header (injected by apiClient);
      // the backend resolves it via middleware, not from this body.
      if (!ENV_TENANT_PINNED && tenantOverride.trim() !== '') {
        setTenantId(tenantOverride.trim())
      }
      const response = await loginRequest(email, password)

      // Module 16: correct password but MFA on - no JWT yet. Park the ticket in
      // component state (never in the auth store) and swap to the code form.
      if (response.isMfaRequired) {
        if (!response.mfaTicket) {
          setError(t('auth.errorGeneric', 'Sign-in failed. Check your credentials.'))
          return
        }
        setMfaTicket(response.mfaTicket)
        setMfaCode('')
        setStep('mfa')
        return
      }

      if (!response.token) {
        setError(t('auth.errorGeneric', 'Sign-in failed. Check your credentials.'))
        return
      }
      login(response.token, response.fullName, response.email)
    } catch (err: unknown) {
      showError(err)
    } finally {
      setLoading(false)
    }
  }

  const handleMfaSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError('')

    try {
      // The store's login() runs ONLY here, on the definitive JWT (tasks.md Frontend item).
      const response = await verifyLoginMfa(mfaTicket, mfaCode.trim())
      if (!response.token) {
        setError(t('auth.errorGeneric', 'Sign-in failed. Check your credentials.'))
        return
      }
      login(response.token, response.fullName, response.email)
    } catch (err: unknown) {
      showError(err)
    } finally {
      setLoading(false)
    }
  }

  const backToPassword = () => {
    setStep('password')
    setMfaTicket('')
    setMfaCode('')
    setPassword('')
    setError('')
  }

  // TOTP (6 digits) or backup code (8 chars once dashes/spaces are stripped).
  const normalizedCode = mfaCode.replace(/[-\s]/g, '')
  const mfaCodeValid = normalizedCode.length >= 6 && normalizedCode.length <= 8

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 sm:px-6 lg:px-8">
      <div className="w-full max-w-md overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg">
        <div className="flex flex-col space-y-1.5 p-6 text-center">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-blue-100">
            {step === 'mfa' ? (
              <ShieldCheck className="h-6 w-6 text-blue-600" />
            ) : (
              <Building2 className="h-6 w-6 text-blue-600" />
            )}
          </div>
          <h3 className="text-2xl font-semibold leading-none tracking-tight">Aspire ERP</h3>
          <p className="text-sm text-slate-500">
            {step === 'mfa'
              ? t('auth.mfaSubtitle', 'Enter the code from your authenticator app')
              : t('auth.subtitle', 'Sign in to your tenant workspace')}
          </p>
        </div>
        <div className="p-6 pt-0">
          {step === 'password' ? (
            <form onSubmit={handlePasswordSubmit} className="space-y-4">
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
          ) : (
            <form onSubmit={handleMfaSubmit} className="space-y-4">
              {error && <div className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</div>}

              <div className="rounded-md bg-sky-50 p-3 text-sm text-sky-800">
                {t('auth.mfaHint', 'Two-factor authentication is enabled for {{email}}. Enter a 6-digit code or a backup code.', { email })}
              </div>

              <div className="space-y-2">
                <label
                  htmlFor="mfa-code"
                  className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                >
                  {t('auth.mfaCode', 'Two-factor code')}
                </label>
                <Input
                  id="mfa-code"
                  inputMode="text"
                  autoComplete="one-time-code"
                  required
                  maxLength={9}
                  className="text-center font-mono text-lg tracking-[0.3em]"
                  placeholder="000000"
                  value={mfaCode}
                  onChange={(e) => setMfaCode(e.target.value)}
                />
                <p className="text-xs text-slate-500">
                  {t('auth.mfaBackupHint', 'Backup codes look like XXXX-XXXX and work once.')}
                </p>
              </div>

              <Button type="submit" className="w-full" disabled={loading || !mfaCodeValid}>
                {loading ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    {t('auth.mfaVerifying', 'Verifying...')}
                  </>
                ) : (
                  t('auth.mfaVerify', 'Verify')
                )}
              </Button>

              <Button type="button" variant="ghost" className="w-full" onClick={backToPassword}>
                <ArrowLeft className="mr-2 h-4 w-4" />
                {t('auth.mfaBack', 'Back to password')}
              </Button>
            </form>
          )}
        </div>
      </div>
    </div>
  )
}
