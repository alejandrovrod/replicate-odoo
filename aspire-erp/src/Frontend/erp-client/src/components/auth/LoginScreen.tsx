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
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-[#faf8f5] px-4 py-12 sm:px-6 lg:px-8">
      {/* Organic Warm Geometric Shapes (Radix UI Theme Playground Motif) */}
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        {/* Soft yellow-amber geometric backdrop patches */}
        <div className="absolute -left-24 -top-24 h-[520px] w-[520px] rounded-[140px] bg-[#fbf3db] opacity-70 blur-3xl transform -rotate-12" />
        <div className="absolute -right-28 top-12 h-[640px] w-[580px] rounded-[180px] bg-[#fbf3db] opacity-80 blur-2xl transform rotate-12" />
        <div className="absolute bottom-[-100px] left-1/4 h-[420px] w-[620px] rounded-[160px] bg-[#fef9eb] opacity-90 blur-2xl transform rotate-6" />

        {/* Crisp polygonal shapes echoing Radix playground art */}
        <svg
          className="absolute inset-0 h-full w-full opacity-40"
          xmlns="http://www.w3.org/2000/svg"
          preserveAspectRatio="none"
          viewBox="0 0 1200 800"
        >
          <polygon points="0,0 280,0 160,520 0,380" fill="#f8eed1" opacity="0.6" />
          <polygon points="780,0 1200,0 1200,600 920,440" fill="#f8eed1" opacity="0.55" />
          <polygon points="1200,520 1200,800 840,800" fill="#f5e8be" opacity="0.4" />
          <polygon points="0,620 320,800 0,800" fill="#f5e8be" opacity="0.45" />
        </svg>
      </div>

      {/* Main Sign-In Card */}
      <div className="relative z-10 w-full max-w-md rounded-2xl border border-amber-200/50 bg-white/95 p-8 shadow-xl shadow-amber-900/5 backdrop-blur-sm transition-all sm:p-10">
        <div className="flex flex-col space-y-1.5 pb-6 text-left">
          <div className="mb-2 flex items-center justify-between">
            <h2 className="text-3xl font-bold tracking-tight text-slate-900">
              {step === 'mfa' ? t('auth.mfaTitle') : t('auth.signInTitle')}
            </h2>
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100 text-amber-800">
              {step === 'mfa' ? (
                <ShieldCheck className="h-5 w-5 text-amber-700" />
              ) : (
                <Building2 className="h-5 w-5 text-amber-700" />
              )}
            </div>
          </div>
          <p className="text-sm text-slate-500">
            {step === 'mfa' ? t('auth.mfaSubtitle') : t('auth.subtitle')}
          </p>
        </div>
        <div>
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

              <div className="flex items-center justify-end gap-3 pt-2">
                <a
                  href="#register"
                  className="rounded-lg px-4 py-2 text-sm font-medium text-amber-900 transition-colors hover:bg-amber-100/60"
                >
                  {t('auth.createAccount', 'Create an account')}
                </a>
                <button
                  type="submit"
                  disabled={loading}
                  className="inline-flex items-center justify-center rounded-lg bg-[#ffd600] px-5 py-2 text-sm font-semibold text-slate-900 shadow-sm transition-all hover:bg-[#ffcc00] hover:shadow focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ffd600] disabled:opacity-50"
                >
                  {loading ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin text-slate-900" />
                      {t('auth.signingIn', 'Signing in...')}
                    </>
                  ) : (
                    t('auth.signIn', 'Sign in')
                  )}
                </button>
              </div>
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

          <div className="mt-6 text-center border-t border-slate-100 pt-4">
            <a
              href="#landing"
              className="text-xs font-semibold text-amber-700 hover:text-amber-800 transition-colors inline-flex items-center gap-1"
            >
              <ArrowLeft className="h-3 w-3" />
              <span>{t('auth.backToLanding', 'Volver a la página principal')}</span>
            </a>
          </div>
        </div>
      </div>
    </div>
  )
}
