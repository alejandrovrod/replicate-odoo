import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, CheckCircle2, Copy, Download, KeyRound, ShieldCheck, ShieldOff, User } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { translateErrorCode } from '../../lib/translateErrorCode'
import {
  useBackupCodes,
  useDisableMfa,
  useProfile,
} from '../auth/api/useProfile'
import { ChangePasswordForm } from './components/ChangePasswordForm'
import { MfaSetupModal } from './components/MfaSetupModal'

type Tab = 'general' | 'security'

/**
 * User profile screen (module 15-user-profile, spec §5): dual-tab layout (General,
 * Security). The Security tab hosts the password form and the MFA lifecycle
 * (setup modal with QR + mandatory backup codes, disable with TOTP proof, and
 * backup-code regeneration which invalidates previous codes).
 */
export function ProfileScreen() {
  const { t, i18n } = useTranslation('common')
  const [tab, setTab] = useState<Tab>('general')
  const [setupOpen, setSetupOpen] = useState(false)
  const [disableCode, setDisableCode] = useState('')
  const [showDisable, setShowDisable] = useState(false)
  const [freshCodes, setFreshCodes] = useState<string[] | null>(null)
  const { profile, status, error, reload } = useProfile()
  const disable = useDisableMfa()
  const backupCodes = useBackupCodes()

  async function handleDisable(e: React.FormEvent) {
    e.preventDefault()
    const result = await disable.run(disableCode.trim())
    if (result !== null) {
      setDisableCode('')
      setShowDisable(false)
      void reload()
    }
  }

  async function handleRegenerate() {
    setFreshCodes(null)
    const result = await backupCodes.run()
    if (result) {
      setFreshCodes(result)
      void reload()
    }
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-4">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">{t('profile.title')}</h2>
        <p className="text-xs text-slate-500">{t('profile.subtitle')}</p>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 rounded-xl border border-slate-200 bg-slate-50 p-1">
        {(['general', 'security'] as const).map((value) => (
          <button
            key={value}
            type="button"
            onClick={() => setTab(value)}
            className={`flex flex-1 items-center justify-center gap-1.5 rounded-lg px-4 py-2 text-sm font-medium transition-colors ${
              tab === value ? 'bg-white text-slate-900 shadow-xs' : 'text-slate-500 hover:text-slate-700'
            }`}
          >
            {value === 'general' ? <User className="h-4 w-4" /> : <KeyRound className="h-4 w-4" />}
            {t(value === 'general' ? 'profile.generalTab' : 'profile.securityTab')}
          </button>
        ))}
      </div>

      {status === 'loading' && (
        <p className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
          {t('state.loading')}
        </p>
      )}

      {status === 'error' && (
        <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{translateErrorCode(i18n, error?.code, error?.message)}</span>
        </div>
      )}

      {profile && tab === 'general' && (
        <div className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-200 text-slate-600">
              <User className="h-5 w-5" />
            </div>
            <div>
              <p className="text-base font-semibold text-slate-900">{profile.fullName}</p>
              <p className="text-xs text-slate-500">{profile.email}</p>
            </div>
          </div>
          <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
            <div>
              <dt className="text-xs font-medium text-slate-500">{t('profile.fullName')}</dt>
              <dd className="mt-0.5 text-slate-900">{profile.fullName}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500">{t('profile.email')}</dt>
              <dd className="mt-0.5 text-slate-900">{profile.email}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500">{t('profile.mfaStatus')}</dt>
              <dd className="mt-0.5">
                {profile.twoFactorEnabled ? (
                  <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700">
                    <ShieldCheck className="h-3.5 w-3.5" />
                    {t('profile.mfaEnabled')}
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-600">
                    <ShieldOff className="h-3.5 w-3.5" />
                    {t('profile.mfaDisabled')}
                  </span>
                )}
              </dd>
            </div>
          </dl>
        </div>
      )}

      {profile && tab === 'security' && (
        <div className="flex flex-col gap-4">
          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
            <ChangePasswordForm />
          </div>

          <div className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">{t('profile.mfaTitle')}</h3>
              <p className="mt-0.5 text-xs text-slate-500">{t('profile.mfaSubtitle')}</p>
            </div>

            {!profile.twoFactorEnabled ? (
              <div>
                <Button onClick={() => setSetupOpen(true)}>
                  <ShieldCheck className="mr-1.5 h-4 w-4" />
                  {t('profile.mfaEnable')}
                </Button>
              </div>
            ) : (
              <>
                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" size="sm" onClick={handleRegenerate} disabled={backupCodes.status === 'loading'}>
                    {t('profile.mfaRegenerate')}
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => setShowDisable((v) => !v)}>
                    <ShieldOff className="mr-1.5 h-3.5 w-3.5" />
                    {t('profile.mfaDisable')}
                  </Button>
                </div>

                {backupCodes.error && (
                  <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                    <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                    <span>{translateErrorCode(i18n, backupCodes.error.code, backupCodes.error.message)}</span>
                  </div>
                )}

                {freshCodes && (
                  <div className="flex flex-col gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3">
                    <p className="text-xs font-medium text-amber-800">{t('profile.mfaRegenerated')}</p>
                    <div className="grid grid-cols-2 gap-1.5 font-mono text-xs text-slate-800">
                      {freshCodes.map((c) => (
                        <span key={c} className="select-all">{c}</span>
                      ))}
                    </div>
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          const blob = new Blob(
                            [`Aspire ERP - MFA backup codes\nGenerated: ${new Date().toISOString()}\n\n${freshCodes.join('\n')}\n`],
                            { type: 'text/plain;charset=utf-8' },
                          )
                          const url = URL.createObjectURL(blob)
                          const anchor = document.createElement('a')
                          anchor.href = url
                          anchor.download = 'aspire-erp-backup-codes.txt'
                          document.body.appendChild(anchor)
                          anchor.click()
                          anchor.remove()
                          URL.revokeObjectURL(url)
                        }}
                      >
                        <Download className="mr-1.5 h-3.5 w-3.5" />
                        {t('profile.setupDownload')}
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => void navigator.clipboard.writeText(freshCodes.join('\n')).catch(() => undefined)}
                      >
                        <Copy className="mr-1.5 h-3.5 w-3.5" />
                        {t('profile.setupCopy')}
                      </Button>
                    </div>
                  </div>
                )}

                {showDisable && (
                  <form onSubmit={handleDisable} className="flex flex-col gap-2 rounded-lg border border-slate-200 bg-slate-50 p-3">
                    <label className="flex flex-col gap-1.5">
                      <span className="text-xs font-medium text-slate-700">{t('profile.mfaDisableCodeLabel')}</span>
                      <Input
                        inputMode="numeric"
                        maxLength={6}
                        placeholder="000000"
                        className="max-w-48 text-center font-mono tracking-[0.3em]"
                        value={disableCode}
                        onChange={(e) => setDisableCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                        required
                      />
                    </label>
                    {disable.error && (
                      <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                        <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                        <span>{translateErrorCode(i18n, disable.error.code, disable.error.message)}</span>
                      </div>
                    )}
                    <div>
                      <Button type="submit" variant="destructive" size="sm" disabled={disableCode.length !== 6 || disable.status === 'loading'}>
                        {t('profile.mfaDisable')}
                      </Button>
                    </div>
                  </form>
                )}
              </>
            )}

            {disable.status === 'success' && !showDisable && (
              <div className="flex items-start gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700">
                <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <span>{t('profile.mfaDisabled')}</span>
              </div>
            )}
          </div>
        </div>
      )}

      <MfaSetupModal open={setupOpen} onClose={() => setSetupOpen(false)} onEnabled={() => void reload()} />
    </div>
  )
}
