import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AlertCircle,
  ArrowLeft,
  Building2,
  CheckCircle2,
  Globe2,
  Loader2,
  Sparkles,
} from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import {
  normalizeLanguage,
  SUPPORTED_LANGUAGES,
  type SupportedLanguage,
} from '../../lib/i18n'
import { translateErrorCode } from '../../lib/translateErrorCode'
import {
  BASE_DOMAIN,
  isReservedSubdomain,
  isSubdomainFormatValid,
  isSubdomainValid,
  normalizeSubdomain,
  tenantUrl,
} from '../../lib/subdomain'
import { useTenantStore } from '../../store/useTenantStore'
import {
  checkSubdomainAvailability,
  registerTenant,
  TenantApiError,
  type TenantRegistrationResult,
} from '../tenants/tenantApi'

interface RegisterTenantScreenProps {
  onBack: () => void
  onSignIn: () => void
}

type Availability = 'idle' | 'checking' | 'available' | 'taken' | 'reserved' | 'error'

const EMPTY_FORM = {
  tenantName: '',
  companyName: '',
  subdomain: '',
  adminFullName: '',
  adminEmail: '',
  adminPassword: '',
  companyTaxId: '',
}

export function RegisterTenantScreen({ onBack, onSignIn }: RegisterTenantScreenProps) {
  const { t, i18n } = useTranslation('landing')
  const setTenantMetadata = useTenantStore((state) => state.setTenantMetadata)
  const setCompanyId = useTenantStore((state) => state.setCompanyId)

  const [form, setForm] = useState(EMPTY_FORM)
  const [defaultLanguage, setDefaultLanguage] = useState<SupportedLanguage>(() =>
    normalizeLanguage(i18n.resolvedLanguage ?? i18n.language),
  )
  const [availability, setAvailability] = useState<Availability>('idle')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [created, setCreated] = useState<TenantRegistrationResult | null>(null)

  const subdomain = normalizeSubdomain(form.subdomain)
  const formatValid = isSubdomainFormatValid(subdomain)
  const reserved = isReservedSubdomain(subdomain)

  useEffect(() => {
    if (!formatValid) {
      setAvailability('idle')
      return
    }

    if (reserved) {
      setAvailability('reserved')
      return
    }

    let cancelled = false
    setAvailability('checking')

    const timer = window.setTimeout(() => {
      void checkSubdomainAvailability(subdomain)
        .then((available) => {
          if (!cancelled) setAvailability(available ? 'available' : 'taken')
        })
        .catch(() => {
          if (!cancelled) setAvailability('error')
        })
    }, 450)

    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [formatValid, reserved, subdomain])

  const update = (field: keyof typeof EMPTY_FORM, value: string) => {
    setForm((current) => ({ ...current, [field]: value }))
    setError(null)
  }

  const canSubmit =
    !submitting &&
    availability === 'available' &&
    isSubdomainValid(subdomain) &&
    form.tenantName.trim().length > 0 &&
    form.companyName.trim().length > 0 &&
    form.adminFullName.trim().length > 0 &&
    form.adminEmail.trim().length > 0 &&
    form.adminPassword.length >= 12

  const availabilityMessage = (() => {
    switch (availability) {
      case 'checking':
        return { tone: 'text-slate-500', text: t('register.subdomain.checking') }
      case 'available':
        return { tone: 'text-emerald-600', text: t('register.subdomain.available') }
      case 'taken':
        return { tone: 'text-red-600', text: t('register.subdomain.taken') }
      case 'reserved':
        return { tone: 'text-amber-700', text: t('register.subdomain.reserved') }
      case 'error':
        return { tone: 'text-red-600', text: t('register.subdomain.error') }
      default:
        return null
    }
  })()

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (!canSubmit) return

    setSubmitting(true)
    setError(null)

    try {
      const result = await registerTenant({
        tenantName: form.tenantName.trim(),
        companyName: form.companyName.trim(),
        subdomain,
        adminFullName: form.adminFullName.trim(),
        adminEmail: form.adminEmail.trim(),
        adminPassword: form.adminPassword,
        companyTaxId: form.companyTaxId.trim() || undefined,
        defaultLanguage,
      })

      setTenantMetadata({
        id: result.tenantId,
        name: result.tenantName,
        subdomain: result.subdomain,
        defaultLanguage: result.defaultLanguage,
      })
      setCompanyId(result.companyId)
      setCreated(result)
    } catch (caught) {
      if (caught instanceof TenantApiError) {
        setError(translateErrorCode(i18n, caught.code, caught.message))
      } else {
        setError(t('register.errors.generic'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (created) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-b from-amber-50 via-slate-50 to-white px-4 py-12">
        <div className="w-full max-w-xl rounded-3xl border border-amber-200/80 bg-white p-8 shadow-xl shadow-amber-100/50 sm:p-10">
          <div className="mb-5 flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-100 text-emerald-700">
            <CheckCircle2 className="h-7 w-7" />
          </div>
          <h1 className="font-heading text-3xl font-extrabold tracking-tight text-slate-950">
            {t('register.success.title')}
          </h1>
          <p className="mt-3 text-sm leading-relaxed text-slate-600">
            {t('register.success.body', { tenant: created.tenantName })}
          </p>

          <div className="mt-6 rounded-2xl border border-slate-200 bg-slate-50 p-4">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t('register.success.workspace')}
            </p>
            <p className="mt-1 break-all font-mono text-sm font-semibold text-slate-900">
              {tenantUrl(created.subdomain)}
            </p>
            <p className="mt-3 text-xs text-slate-500">
              {t('register.success.signInAs', { email: created.adminEmail })}
            </p>
          </div>

          <div className="mt-7 flex flex-col gap-3 sm:flex-row">
            <Button className="flex-1" onClick={onSignIn}>
              {t('register.success.signIn')}
            </Button>
            <Button className="flex-1" variant="outline" onClick={onBack}>
              {t('register.success.back')}
            </Button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-amber-50 via-slate-50 to-white">
      <div className="mx-auto grid min-h-screen w-full max-w-6xl gap-10 px-4 py-10 lg:grid-cols-[0.9fr_1.1fr] lg:items-center lg:px-8">
        <aside className="hidden lg:block">
          <button
            type="button"
            onClick={onBack}
            className="mb-10 inline-flex items-center gap-2 text-sm font-medium text-slate-600 transition hover:text-slate-950"
          >
            <ArrowLeft className="h-4 w-4" />
            {t('register.back')}
          </button>

          <div className="inline-flex items-center gap-2 rounded-full border border-amber-200 bg-white px-3 py-1 text-xs font-semibold text-amber-800 shadow-xs">
            <Sparkles className="h-3.5 w-3.5" />
            {t('register.badge')}
          </div>

          <h1 className="mt-6 font-heading text-4xl font-extrabold tracking-tight text-slate-950">
            {t('register.title')}
          </h1>
          <p className="mt-4 max-w-md text-base leading-relaxed text-slate-600">
            {t('register.subtitle')}
          </p>

          <ul className="mt-8 space-y-4">
            {[t('register.benefits.workspace'), t('register.benefits.coa'), t('register.benefits.security')].map(
              (benefit) => (
                <li key={benefit} className="flex items-start gap-3 text-sm text-slate-700">
                  <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                  <span>{benefit}</span>
                </li>
              ),
            )}
          </ul>
        </aside>

        <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-xl shadow-slate-200/60 sm:p-8">
          <button
            type="button"
            onClick={onBack}
            className="mb-5 inline-flex items-center gap-2 text-sm font-medium text-slate-600 transition hover:text-slate-950 lg:hidden"
          >
            <ArrowLeft className="h-4 w-4" />
            {t('register.back')}
          </button>

          <div className="mb-6 flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-amber-100 text-amber-700">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <h2 className="font-heading text-xl font-bold text-slate-950">{t('register.formTitle')}</h2>
              <p className="text-xs text-slate-500">{t('register.formSubtitle')}</p>
            </div>
          </div>

          {error ? (
            <div
              role="alert"
              className="mb-5 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700"
            >
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          ) : null}

          <form className="space-y-4" onSubmit={handleSubmit} noValidate>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t('register.fields.tenantName')} htmlFor="tenant-name">
                <Input
                  id="tenant-name"
                  value={form.tenantName}
                  onChange={(event) => update('tenantName', event.target.value)}
                  placeholder={t('register.placeholders.tenantName')}
                  maxLength={100}
                  autoComplete="organization"
                  required
                />
              </Field>

              <Field label={t('register.fields.companyName')} htmlFor="company-name">
                <Input
                  id="company-name"
                  value={form.companyName}
                  onChange={(event) => update('companyName', event.target.value)}
                  placeholder={t('register.placeholders.companyName')}
                  maxLength={150}
                  required
                />
              </Field>
            </div>

            <div>
              <p
                id="default-language-label"
                className="mb-1.5 block text-xs font-semibold text-slate-700"
              >
                {t('register.fields.defaultLanguage')}
              </p>
              <div
                role="radiogroup"
                aria-labelledby="default-language-label"
                className="grid grid-cols-2 gap-2"
              >
                {SUPPORTED_LANGUAGES.map((language) => {
                  const selected = defaultLanguage === language
                  return (
                    <button
                      key={language}
                      type="button"
                      role="radio"
                      aria-checked={selected}
                      onClick={() => setDefaultLanguage(language)}
                      className={`flex items-center justify-center gap-2 rounded-xl border px-3 py-2.5 text-sm font-semibold transition ${
                        selected
                          ? 'border-amber-400 bg-amber-50 text-amber-900 shadow-xs'
                          : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50'
                      }`}
                    >
                      <span>{language === 'es' ? 'Español' : 'English'}</span>
                      <span
                        className={`rounded-md px-1.5 py-0.5 font-mono text-[10px] font-bold ${
                          selected
                            ? 'bg-amber-200/70 text-amber-900'
                            : 'bg-slate-100 text-slate-500'
                        }`}
                      >
                        {language.toUpperCase()}
                      </span>
                    </button>
                  )
                })}
              </div>
              <p className="mt-1.5 text-xs text-slate-500">
                {t('register.languageHint')}
              </p>
            </div>

            <Field label={t('register.fields.subdomain')} htmlFor="subdomain">
              <div className="flex overflow-hidden rounded-md border border-slate-200 bg-white focus-within:border-slate-400 focus-within:ring-2 focus-within:ring-slate-200">
                <Input
                  id="subdomain"
                  value={form.subdomain}
                  onChange={(event) => update('subdomain', event.target.value.toLowerCase())}
                  placeholder={t('register.placeholders.subdomain')}
                  maxLength={32}
                  autoCapitalize="none"
                  autoCorrect="off"
                  spellCheck={false}
                  className="rounded-none border-0 focus-visible:ring-0"
                  required
                />
                <span className="flex items-center gap-1 border-l border-slate-200 bg-slate-50 px-3 font-mono text-xs text-slate-500">
                  <Globe2 className="h-3.5 w-3.5" />.{BASE_DOMAIN}
                </span>
              </div>
              <div className="mt-1.5 min-h-5 text-xs" aria-live="polite">
                {availabilityMessage ? (
                  <span className={availabilityMessage.tone}>{availabilityMessage.text}</span>
                ) : (
                  <span className="text-slate-500">{t('register.subdomain.hint')}</span>
                )}
              </div>
            </Field>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t('register.fields.adminName')} htmlFor="admin-name">
                <Input
                  id="admin-name"
                  value={form.adminFullName}
                  onChange={(event) => update('adminFullName', event.target.value)}
                  placeholder={t('register.placeholders.adminName')}
                  autoComplete="name"
                  maxLength={200}
                  required
                />
              </Field>

              <Field label={t('register.fields.taxId')} htmlFor="tax-id">
                <Input
                  id="tax-id"
                  value={form.companyTaxId}
                  onChange={(event) => update('companyTaxId', event.target.value)}
                  placeholder={t('register.placeholders.taxId')}
                  maxLength={50}
                />
              </Field>
            </div>

            <Field label={t('register.fields.adminEmail')} htmlFor="admin-email">
              <Input
                id="admin-email"
                type="email"
                value={form.adminEmail}
                onChange={(event) => update('adminEmail', event.target.value)}
                placeholder={t('register.placeholders.adminEmail')}
                autoComplete="email"
                maxLength={256}
                required
              />
            </Field>

            <Field label={t('register.fields.password')} htmlFor="admin-password">
              <Input
                id="admin-password"
                type="password"
                value={form.adminPassword}
                onChange={(event) => update('adminPassword', event.target.value)}
                placeholder={t('register.placeholders.password')}
                autoComplete="new-password"
                minLength={12}
                maxLength={128}
                required
              />
              <p className="mt-1.5 text-xs text-slate-500">{t('register.passwordHint')}</p>
            </Field>

            <Button type="submit" className="mt-2 w-full" size="lg" disabled={!canSubmit}>
              {submitting ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  {t('register.submitting')}
                </>
              ) : (
                t('register.submit')
              )}
            </Button>
          </form>

          <p className="mt-5 text-center text-xs text-slate-500">
            {t('register.alreadyHaveAccount')}{' '}
            <button
              type="button"
              onClick={onSignIn}
              className="font-semibold text-amber-700 underline-offset-4 hover:underline"
            >
              {t('register.signIn')}
            </button>
          </p>
        </section>
      </div>
    </div>
  )
}

function Field({
  label,
  htmlFor,
  children,
}: {
  label: string
  htmlFor: string
  children: ReactNode
}) {
  return (
    <div>
      <label htmlFor={htmlFor} className="mb-1.5 block text-xs font-semibold text-slate-700">
        {label}
      </label>
      {children}
    </div>
  )
}
