import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, Copy } from 'lucide-react'
import { Button } from '../../components/ui/Button'

/** One-time credential display for user creation / password reset. */
export function TemporaryPasswordPanel({ password }: { password: string }) {
  const { t } = useTranslation('security')
  const [copied, setCopied] = useState(false)

  const copy = () => {
    void navigator.clipboard?.writeText(password).then(
      () => setCopied(true),
      () => setCopied(false),
    )
  }

  return (
    <div
      role="status"
      className="space-y-2 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900"
    >
      <p className="font-medium">{t('users.passwordTitle', 'Temporary password')}</p>
      <p className="text-xs">{t('users.passwordBody', 'Copy it now: it is shown only once.')}</p>
      <div className="flex items-center gap-2">
        <code
          data-testid="temporary-password"
          className="flex-1 rounded border border-emerald-200 bg-white px-2 py-1.5 font-mono text-sm"
        >
          {password}
        </code>
        <Button type="button" size="sm" variant="outline" onClick={copy}>
          {copied ? <Check className="mr-1 h-3.5 w-3.5" /> : <Copy className="mr-1 h-3.5 w-3.5" />}
          {copied
            ? t('users.passwordCopied', 'Copied')
            : t('users.passwordCopy', 'Copy')}
        </Button>
      </div>
    </div>
  )
}
