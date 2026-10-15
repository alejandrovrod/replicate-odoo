/**
 * Role assignment dialog for a single user (SEC-G3 / SEC-G5).
 *
 * Ticking roles builds the full assignment set sent with
 * `PUT /api/v1/users/{id}/roles`. A 409 `CANNOT_REMOVE_LAST_ADMIN` (removing
 * System Manager from the tenant's last administrator) renders as a semantic
 * alert instead of a generic failure. SEC-G6: assignments apply on next login.
 */
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { Button } from '../../components/ui/Button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../components/ui/Dialog'
import { assignUserRolesApi, type RoleDto, type UserWithRolesDto } from './securityApi'

interface UserRolesModalProps {
  user: UserWithRolesDto | null
  roles: RoleDto[]
  onClose: () => void
  onSaved: () => void
}

export function UserRolesModal({ user, roles, onClose, onSaved }: UserRolesModalProps) {
  const { t, i18n } = useTranslation('security')
  const { t: tCommon } = useTranslation('common')
  const [selected, setSelected] = useState<string[]>([])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)
  const [notice, setNotice] = useState(false)

  useEffect(() => {
    setSelected(user?.roleIds ?? [])
    setError(null)
    setNotice(false)
  }, [user])

  if (!user) return null

  const toggle = (roleId: string) => {
    setSelected((prev) =>
      prev.includes(roleId) ? prev.filter((id) => id !== roleId) : [...prev, roleId],
    )
    setError(null)
    setNotice(false)
  }

  const handleSave = async () => {
    setSaving(true)
    setError(null)
    try {
      await assignUserRolesApi(user.id, selected)
      setNotice(true)
      onSaved()
    } catch (cause) {
      setError(
        cause instanceof ApiError ? cause : new ApiError(0, 'Update failed', String(cause)),
      )
    } finally {
      setSaving(false)
    }
  }

  const isLastAdminError = error?.code === 'CANNOT_REMOVE_LAST_ADMIN'

  return (
    <Dialog open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('userModal.title', 'Roles for {{email}}', { email: user.email })}</DialogTitle>
          <DialogDescription>{t('userModal.subtitle', '')}</DialogDescription>
        </DialogHeader>

        <div className="space-y-2 overflow-y-auto">
          <p className="text-sm font-medium text-slate-700">{user.fullName}</p>
          {roles.map((role) => (
            <label
              key={role.id}
              className="flex cursor-pointer items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-sm hover:bg-slate-50"
            >
              <input
                id={`user-role-check-${role.id}`}
                type="checkbox"
                className="h-4 w-4 accent-sky-600"
                checked={selected.includes(role.id)}
                onChange={() => toggle(role.id)}
                aria-label={`${role.name} — ${user.email}`}
              />
              <span className="flex-1">{role.name}</span>
              {role.isSystemDefault && (
                <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold text-slate-600">
                  {t('role.systemBadge', 'System')}
                </span>
              )}
            </label>
          ))}
        </div>

        {isLastAdminError ? (
          <div role="alert" className="rounded-md border border-red-300 bg-red-50 p-3 text-sm font-medium text-red-800">
            {t('userModal.lastAdmin', '')}
          </div>
        ) : (
          error && (
            <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
              {translateErrorCode(
                i18n,
                error.code,
                t('userModal.failed', 'Could not update roles: {{message}}', {
                  message: error.message,
                }),
              )}
            </div>
          )
        )}
        {notice && <p className="text-sm text-emerald-700">{t('userModal.saved', 'Roles updated.')}</p>}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {tCommon('action.cancel', 'Cancel')}
          </Button>
          <Button onClick={() => void handleSave()} disabled={saving}>
            {saving ? t('userModal.saving', 'Saving…') : t('userModal.save', 'Save roles')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
