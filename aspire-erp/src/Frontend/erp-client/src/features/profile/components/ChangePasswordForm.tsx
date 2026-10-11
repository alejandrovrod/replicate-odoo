import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CheckCircle2, AlertTriangle } from 'lucide-react'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useChangePassword } from '../../auth/api/useProfile'

/**
 * Password change form (module 15-user-profile, spec §5 Security tab).
 * Backend Domain Error Codes (`AUTH_*`) resolve through the `error` namespace via
 * `translateErrorCode`, falling back to the already-localized ProblemDetails detail.
 */
export function ChangePasswordForm({ onChanged }: { onChanged?: () => void }) {
  const { t, i18n } = useTranslation('common')
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [done, setDone] = useState(false)
  const mutation = useChangePassword()

  const mismatch = confirmPassword !== '' && newPassword !== confirmPassword

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setDone(false)
    const result = await mutation.run({ currentPassword, newPassword, confirmPassword })
    if (result !== null) {
      setDone(true)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      onChanged?.()
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div>
        <h3 className="text-sm font-semibold text-slate-900">{t('profile.passwordTitle')}</h3>
        <p className="mt-0.5 text-xs text-slate-500">{t('profile.passwordSubtitle')}</p>
      </div>

      <label className="flex flex-col gap-1.5">
        <span className="text-xs font-medium text-slate-700">{t('profile.currentPassword')}</span>
        <Input
          type="password"
          autoComplete="current-password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
          required
        />
      </label>

      <label className="flex flex-col gap-1.5">
        <span className="text-xs font-medium text-slate-700">{t('profile.newPassword')}</span>
        <Input
          type="password"
          autoComplete="new-password"
          minLength={12}
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          required
        />
      </label>

      <label className="flex flex-col gap-1.5">
        <span className="text-xs font-medium text-slate-700">{t('profile.confirmPassword')}</span>
        <Input
          type="password"
          autoComplete="new-password"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          required
        />
        {mismatch && (
          <span className="text-xs text-red-600">{t('validation.invalid', 'Invalid value')}</span>
        )}
      </label>

      {mutation.error && (
        <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>{translateErrorCode(i18n, mutation.error.code, mutation.error.message)}</span>
        </div>
      )}

      {done && (
        <div className="flex items-start gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700">
          <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>{t('profile.passwordChanged')}</span>
        </div>
      )}

      <div>
        <Button type="submit" disabled={mutation.status === 'loading' || mismatch}>
          {mutation.status === 'loading' ? t('state.saving') : t('action.save')}
        </Button>
      </div>
    </form>
  )
}
