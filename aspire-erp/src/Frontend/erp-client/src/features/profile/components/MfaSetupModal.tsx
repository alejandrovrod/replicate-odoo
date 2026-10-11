import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { QRCodeSVG } from 'qrcode.react'
import { AlertTriangle, CheckCircle2, Copy, Download } from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useEnableMfa, useVerifyMfa, type EnableMfaPayload } from '../../auth/api/useProfile'

interface MfaSetupModalProps {
  open: boolean
  onClose: () => void
  /** Called after the TOTP code finalizes enrollment so the screen refreshes MFA status. */
  onEnabled: () => void
}

function downloadCodes(codes: string[]) {
  const blob = new Blob(
    [`Aspire ERP - MFA backup codes\nGenerated: ${new Date().toISOString()}\n\n${codes.join('\n')}\n`],
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
}

/**
 * Guided MFA enrollment (module 15-user-profile, spec §5 MFA flow): QR code for TOTP apps,
 * 6-digit verification input, and a MANDATORY backup-codes step - the verify action stays
 * disabled until the user downloads or copies the codes AND confirms they saved them.
 */
export function MfaSetupModal({ open, onClose, onEnabled }: MfaSetupModalProps) {
  const { t, i18n } = useTranslation('common')
  const [payload, setPayload] = useState<EnableMfaPayload | null>(null)
  const [code, setCode] = useState('')
  const [saved, setSaved] = useState(false)
  const [secured, setSecured] = useState(false) // downloaded || copied
  const [copied, setCopied] = useState(false)
  const [finished, setFinished] = useState(false)
  const enable = useEnableMfa()
  const verify = useVerifyMfa()

  // Staging starts when the modal opens: the secret + initial codes are minted once.
  useEffect(() => {
    if (!open) return
    setPayload(null)
    setCode('')
    setSaved(false)
    setSecured(false)
    setCopied(false)
    setFinished(false)
    verify.reset()
    void enable.run().then((result) => {
      if (result) setPayload(result)
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open ])

  function handleClose() {
    if (verify.status === 'loading') return
    onClose()
  }

  async function handleCopy() {
    if (!payload) return
    try {
      await navigator.clipboard.writeText(payload.recoveryCodes.join('\n'))
      setCopied(true)
      setSecured(true)
    } catch {
      // Clipboard unavailable (permissions / non-secure context): the download path covers it.
    }
  }

  function handleDownload() {
    if (!payload) return
    downloadCodes(payload.recoveryCodes)
    setSecured(true)
  }

  async function handleVerify(e: React.FormEvent) {
    e.preventDefault()
    const result = await verify.run(code.trim())
    if (result !== null) {
      setFinished(true)
      onEnabled()
    }
  }

  const canVerify = saved && secured && code.trim().length === 6 && verify.status !== 'loading'

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('profile.setupTitle')}</DialogTitle>
          <DialogDescription>{t('profile.mfaSubtitle')}</DialogDescription>
        </DialogHeader>

        {enable.status === 'loading' || (!payload && !enable.error) ? (
          <p className="py-8 text-center text-sm text-slate-500">{t('state.loading')}</p>
        ) : enable.error || !payload ? (
          <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
            <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            <span>{translateErrorCode(i18n, enable.error?.code, enable.error?.message)}</span>
          </div>
        ) : finished ? (
          <div className="flex flex-col items-center gap-3 py-6 text-center">
            <CheckCircle2 className="h-10 w-10 text-emerald-600" />
            <p className="text-sm font-medium text-slate-900">{t('profile.setupEnabled')}</p>
            <Button onClick={handleClose}>{t('action.close')}</Button>
          </div>
        ) : (
          <form onSubmit={handleVerify} className="flex flex-col gap-5 overflow-y-auto">
            {/* Step 1: QR scan */}
            <section className="flex flex-col items-center gap-2">
              <p className="w-full text-xs font-medium text-slate-700">
                {t('profile.setupStepScan')}
              </p>
              <div className="rounded-xl border border-slate-200 bg-white p-3">
                <QRCodeSVG value={payload.authenticatorUri} size={180} />
              </div>
              <details className="w-full text-xs text-slate-500">
                <summary className="cursor-pointer text-sky-700 hover:underline">
                  {t('profile.setupManualKey')}
                </summary>
                <code className="mt-1 block rounded bg-slate-100 px-2 py-1 font-mono text-[11px] break-all select-all">
                  {payload.sharedKey}
                </code>
              </details>
            </section>

            {/* Step 2: mandatory backup codes */}
            <section className="flex flex-col gap-2">
              <p className="text-xs font-medium text-slate-700">{t('profile.setupStepBackup')}</p>
              <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                {t('profile.setupBackupWarning')}
              </div>
              <div className="grid grid-cols-2 gap-1.5 rounded-lg border border-slate-200 bg-slate-50 p-3 font-mono text-xs text-slate-800">
                {payload.recoveryCodes.map((c) => (
                  <span key={c} className="select-all">
                    {c}
                  </span>
                ))}
              </div>
              <div className="flex gap-2">
                <Button type="button" variant="outline" size="sm" onClick={handleDownload}>
                  <Download className="mr-1.5 h-3.5 w-3.5" />
                  {t('profile.setupDownload')}
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={handleCopy}>
                  <Copy className="mr-1.5 h-3.5 w-3.5" />
                  {copied ? t('profile.setupCopied') : t('profile.setupCopy')}
                </Button>
              </div>
              <label className="flex cursor-pointer items-start gap-2 text-xs text-slate-700">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 accent-sky-600"
                  checked={saved}
                  onChange={(e) => setSaved(e.target.checked)}
                />
                {t('profile.setupConfirmSaved')}
              </label>
            </section>

            {/* Step 3: verify */}
            <section className="flex flex-col gap-2">
              <p className="text-xs font-medium text-slate-700">{t('profile.setupStepVerify')}</p>
              <Input
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                placeholder="000000"
                className="text-center font-mono text-lg tracking-[0.5em]"
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                required
              />
              {verify.error && (
                <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>{translateErrorCode(i18n, verify.error.code, verify.error.message)}</span>
                </div>
              )}
              <Button type="submit" disabled={!canVerify}>
                {verify.status === 'loading' ? t('state.saving') : t('profile.setupVerifyAndEnable')}
              </Button>
            </section>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
