import { useEffect, useState, type ReactNode } from 'react'
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
import { Input } from '../../components/ui/Input'
import { Switch } from '../../components/ui/Switch'
import {
  createUserApi,
  updateUserApi,
  type RoleDto,
  type UserAdminDto,
} from './securityApi'
import { TemporaryPasswordPanel } from './TemporaryPasswordPanel'

const MAX_FULL_NAME = 200
const MAX_EMAIL = 256
const MIN_PASSWORD = 12
const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/

export interface UserDraft {
  fullName: string
  email: string
  temporaryPassword: string
  roleIds: string[]
  isActive: boolean
}

/** Client mirror of the domain rules; returned as i18n keys so the UI stays localized. */
export function userDraftViolations(draft: UserDraft, isEdit: boolean): string[] {
  const violations: string[] = []
  const fullName = draft.fullName.trim()
  if (fullName.length === 0) violations.push('userForm.errorFullNameRequired')
  else if (fullName.length > MAX_FULL_NAME) violations.push('userForm.errorFullNameTooLong')

  if (!isEdit) {
    const email = draft.email.trim()
    if (email.length === 0 || email.length > MAX_EMAIL || !EMAIL_PATTERN.test(email)) {
      violations.push('userForm.errorEmailInvalid')
    }

    if (draft.temporaryPassword.length > 0) {
      const password = draft.temporaryPassword
      const hasUpper = /[A-Z]/.test(password)
      const hasLower = /[a-z]/.test(password)
      const hasDigit = /\d/.test(password)
      if (password.length < MIN_PASSWORD || !hasUpper || !hasLower || !hasDigit) {
        violations.push('userForm.errorPasswordPolicy')
      }
    }
  }

  if (draft.roleIds.length === 0) violations.push('userForm.errorRolesRequired')
  return violations
}

interface Props {
  open: boolean
  /** Null = create mode; a user opens the edit mode with its rowversion token. */
  user: UserAdminDto | null
  roles: RoleDto[]
  onClose: () => void
  /** Fired after the server persisted the user (create or update). */
  onSaved: (user: UserAdminDto) => void
}

function initialDraft(user: UserAdminDto | null): UserDraft {
  return {
    fullName: user?.fullName ?? '',
    email: user?.email ?? '',
    temporaryPassword: '',
    roleIds: user?.roleIds ?? [],
    isActive: user?.isActive ?? true,
  }
}

export function UserFormModal({ open, user, roles, onClose, onSaved }: Props) {
  const { t, i18n } = useTranslation('security')
  const { t: tCommon } = useTranslation('common')
  const [draft, setDraft] = useState<UserDraft>(() => initialDraft(user))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)
  const [createdPassword, setCreatedPassword] = useState<string | null>(null)

  const isEdit = user !== null

  useEffect(() => {
    if (open) {
      setDraft(initialDraft(user))
      setError(null)
      setCreatedPassword(null)
    }
  }, [open, user])

  const update = (patch: Partial<UserDraft>) => {
    setDraft((prev) => ({ ...prev, ...patch }))
    setError(null)
  }

  const toggleRole = (roleId: string) => {
    update({
      roleIds: draft.roleIds.includes(roleId)
        ? draft.roleIds.filter((id) => id !== roleId)
        : [...draft.roleIds, roleId],
    })
  }

  const violations = userDraftViolations(draft, isEdit)

  const handleSave = async () => {
    if (violations.length > 0) return
    setSaving(true)
    setError(null)

    try {
      if (user) {
        const saved = await updateUserApi(user.id, {
          fullName: draft.fullName.trim(),
          roleIds: draft.roleIds,
          isActive: draft.isActive,
          rowVersion: user.rowVersion,
        })
        onSaved(saved)
        onClose()
      } else {
        const created = await createUserApi({
          fullName: draft.fullName.trim(),
          email: draft.email.trim(),
          temporaryPassword: draft.temporaryPassword.trim() || null,
          roleIds: draft.roleIds,
          isActive: draft.isActive,
        })
        setCreatedPassword(created.temporaryPassword)
        onSaved(created.user)
      }
    } catch (cause) {
      setError(cause instanceof ApiError ? cause : new ApiError(0, 'Save failed', String(cause)))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose()
      }}
    >
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {isEdit
              ? t('userForm.editTitle', 'Edit user')
              : t('userForm.createTitle', 'New user')}
          </DialogTitle>
          <DialogDescription>
            {isEdit
              ? t('userForm.editDescription', 'Update the display name, roles and status.')
              : t(
                  'userForm.createDescription',
                  'The user signs in with this email and the temporary password.',
                )}
          </DialogDescription>
        </DialogHeader>

        {createdPassword ? (
          <div className="space-y-4">
            <p className="text-sm text-slate-700">
              {t('userForm.created', 'User created. Share these credentials securely.')}
            </p>
            <TemporaryPasswordPanel password={createdPassword} />
          </div>
        ) : (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label={t('userForm.fullName', 'Full name')}>
                <Input
                  id="user-form-full-name"
                  value={draft.fullName}
                  onChange={(e) => update({ fullName: e.target.value })}
                  disabled={saving}
                  maxLength={MAX_FULL_NAME}
                  autoFocus
                />
              </Field>

              <Field label={t('userForm.email', 'Email')}>
                <Input
                  id="user-form-email"
                  type="email"
                  value={draft.email}
                  onChange={(e) => update({ email: e.target.value })}
                  disabled={saving || isEdit}
                  readOnly={isEdit}
                  maxLength={MAX_EMAIL}
                />
              </Field>

              {!isEdit && (
                <Field
                  label={t('userForm.temporaryPassword', 'Temporary password (optional)')}
                  hint={t(
                    'userForm.temporaryPasswordHint',
                    'Leave empty to generate a secure password.',
                  )}
                >
                  <Input
                    id="user-form-temp-password"
                    type="text"
                    autoComplete="new-password"
                    value={draft.temporaryPassword}
                    onChange={(e) => update({ temporaryPassword: e.target.value })}
                    disabled={saving}
                  />
                </Field>
              )}

              <div className="flex items-center gap-3 rounded-md border border-slate-200 px-3 py-2 sm:col-span-2">
                <Switch
                  id="user-form-active"
                  checked={draft.isActive}
                  onCheckedChange={(checked) => update({ isActive: checked })}
                  disabled={saving}
                />
                <div>
                  <label htmlFor="user-form-active" className="text-sm font-medium text-slate-700">
                    {t('userForm.active', 'Active account')}
                  </label>
                  <p className="text-[11px] text-slate-500">
                    {t('userForm.activeHint', 'Inactive users cannot sign in.')}
                  </p>
                </div>
              </div>
            </div>

            <section className="space-y-2">
              <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                {t('userForm.roles', 'Roles')}
              </h4>
              <p className="text-[11px] text-slate-500">
                {t('userForm.rolesHint', 'Pick at least one role.')}
              </p>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                {roles.map((role) => (
                  <label
                    key={role.id}
                    className="flex cursor-pointer items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-sm hover:bg-amber-50/40"
                  >
                    <input
                      id={`user-role-check-${role.id}`}
                      type="checkbox"
                      className="h-4 w-4 accent-amber-600"
                      checked={draft.roleIds.includes(role.id)}
                      onChange={() => toggleRole(role.id)}
                      disabled={saving}
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
            </section>

            {violations.length > 0 && (
              <ul className="list-disc pl-4 text-xs text-rose-600" aria-live="polite">
                {violations.map((key) => (
                  <li key={key}>{t(key as 'userForm.errorFullNameRequired')}</li>
                ))}
              </ul>
            )}

            {error && error.code === 'CANNOT_REMOVE_LAST_ADMIN' ? (
              <div
                role="alert"
                className="rounded-md border border-red-300 bg-red-50 p-3 text-sm font-medium text-red-800"
              >
                {t('userForm.lastAdmin', '')}
              </div>
            ) : (
              error && (
                <div
                  role="alert"
                  className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800"
                >
                  {translateErrorCode(
                    i18n,
                    error.code,
                    t('userForm.failed', 'Could not save the user: {{message}}', {
                      message: error.message,
                    }),
                  )}
                </div>
              )
            )}
          </div>
        )}

        <DialogFooter className="border-t border-slate-100 pt-4">
          <Button type="button" variant="outline" onClick={onClose} disabled={saving}>
            {createdPassword ? tCommon('action.close', 'Close') : tCommon('action.cancel', 'Cancel')}
          </Button>
          {!createdPassword && (
            <Button
              id="user-form-save"
              type="button"
              onClick={() => void handleSave()}
              disabled={saving || violations.length > 0}
            >
              {saving
                ? t('userForm.saving', 'Saving…')
                : isEdit
                  ? t('userForm.save', 'Save changes')
                  : t('userForm.create', 'Create user')}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function Field({
  label,
  hint,
  children,
}: {
  label: string
  hint?: string
  children: ReactNode
}) {
  return (
    <label className="block text-xs font-medium text-slate-600">
      {label}
      <div className="mt-1">{children}</div>
      {hint && <p className="mt-1 text-[11px] font-normal text-slate-500">{hint}</p>}
    </label>
  )
}
