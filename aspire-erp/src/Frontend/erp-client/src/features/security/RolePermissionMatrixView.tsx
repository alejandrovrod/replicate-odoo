/**
 * Role × document-type permission matrix.
 *
 * Rows are DocTypes grouped by module (spec §5 catalog), columns are the six
 * actions. Includes per-row and per-column "select all" toggles, a reactive
 * `Create ⇒ Write` invariant (see `useRolePermissions`), a save confirmation,
 * an active-users warning (`userCount > 0`, SEC-G6), and a locked read-only
 * rendering for the System Manager role (SEC-G5: no rows, 403 on update).
 */
import { useState } from 'react'
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/Table'
import {
  DOC_TYPES_CATALOG,
  DOC_TYPE_MODULES,
  PERMISSION_ACTIONS,
  type DocTypeModule,
  type PermissionAction,
} from './docTypesCatalog'
import { createRoleApi } from './securityApi'
import { useRolePermissions } from './useRolePermissions'

export function RolePermissionMatrixView() {
  const { t, i18n } = useTranslation('security')
  const { t: tCommon } = useTranslation('common')
  const perms = useRolePermissions()
  const [confirmOpen, setConfirmOpen] = useState(false)

  const [newName, setNewName] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [creating, setCreating] = useState(false)
  const [createError, setCreateError] = useState<ApiError | null>(null)
  const [createNotice, setCreateNotice] = useState(false)

  const actionLabel = (action: PermissionAction): string =>
    t(`matrix.col.${action}`, action)

  const groupLabel = (module: DocTypeModule): string => {
    const fallback = DOC_TYPE_MODULES.find((m) => m.id === module)?.label ?? module
    return t(`matrix.group.${module}`, fallback)
  }

  const handleCreate = async () => {
    const name = newName.trim()
    if (!name || creating) return
    setCreating(true)
    setCreateError(null)
    setCreateNotice(false)
    try {
      const created = await createRoleApi({
        name,
        description: newDescription.trim() || undefined,
      })
      setNewName('')
      setNewDescription('')
      setCreateNotice(true)
      await perms.reloadRoles()
      perms.selectRole(created.id)
    } catch (cause) {
      setCreateError(
        cause instanceof ApiError ? cause : new ApiError(0, 'Create failed', String(cause)),
      )
    } finally {
      setCreating(false)
    }
  }

  return (
    <div className="space-y-4">
      {/* Role selector + create */}
      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
        <div className="flex flex-wrap items-end gap-3">
          <div className="min-w-56 flex-1">
            <label htmlFor="role-select" className="mb-1 block text-xs font-semibold text-slate-600">
              {t('role.label', 'Role')}
            </label>
            {perms.rolesLoading ? (
              <div className="h-10 animate-pulse rounded-md bg-slate-100" aria-hidden="true" />
            ) : (
              <select
                id="role-select"
                className="h-10 w-full rounded-md border border-slate-200 bg-white px-3 text-sm focus:ring-2 focus:ring-sky-600 focus:outline-none"
                value={perms.selectedRoleId ?? ''}
                onChange={(e) => perms.selectRole(e.target.value)}
                disabled={perms.roles.length === 0}
              >
                {perms.roles.length === 0 && (
                  <option value="">{t('role.selectPlaceholder', 'Select a role…')}</option>
                )}
                {perms.roles.map((role) => (
                  <option key={role.id} value={role.id}>
                    {role.name}
                    {role.isSystemDefault ? ` (${t('role.systemBadge', 'System')})` : ''}
                    {typeof role.userCount === 'number'
                      ? ` — ${t('role.users_other', '{{count}} users', { count: role.userCount })}`
                      : ''}
                  </option>
                ))}
              </select>
            )}
          </div>
          <div className="flex flex-1 flex-wrap items-end gap-2">
            <div>
              <label htmlFor="new-role-name" className="mb-1 block text-xs font-semibold text-slate-600">
                {t('role.nameLabel', 'Name')}
              </label>
              <input
                id="new-role-name"
                className="h-10 rounded-md border border-slate-200 px-3 text-sm focus:ring-2 focus:ring-sky-600 focus:outline-none"
                placeholder={t('role.namePlaceholder', 'e.g. Support L1')}
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="new-role-desc" className="mb-1 block text-xs font-semibold text-slate-600">
                {t('role.descriptionLabel', 'Description')}
              </label>
              <input
                id="new-role-desc"
                className="h-10 rounded-md border border-slate-200 px-3 text-sm focus:ring-2 focus:ring-sky-600 focus:outline-none"
                placeholder={t('role.descriptionPlaceholder', 'What this role is for (optional)')}
                value={newDescription}
                onChange={(e) => setNewDescription(e.target.value)}
              />
            </div>
            <Button onClick={() => void handleCreate()} disabled={!newName.trim() || creating}>
              {creating ? t('role.creating', 'Creating…') : t('role.createBtn', 'Create role')}
            </Button>
          </div>
        </div>
        {perms.rolesError && (
          <div role="alert" className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
            {translateErrorCode(
              i18n,
              perms.rolesError.code,
              t('role.loadFailed', 'Could not load roles: {{message}}', {
                message: perms.rolesError.message,
              }),
            )}
            <div className="mt-2">
              <Button size="sm" variant="outline" onClick={() => void perms.reloadRoles()}>
                {t('role.retry', 'Retry')}
              </Button>
            </div>
          </div>
        )}
        {createError && (
          <div role="alert" className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
            {translateErrorCode(
              i18n,
              createError.code,
              t('role.createFailed', 'Could not create the role: {{message}}', {
                message: createError.message,
              }),
            )}
          </div>
        )}
        {createNotice && (
          <p className="mt-3 text-sm text-emerald-700">{t('role.created', 'Role created.')}</p>
        )}
        {!perms.rolesLoading && !perms.rolesError && perms.roles.length === 0 && (
          <p className="mt-3 text-sm text-slate-500">{t('role.empty', 'No roles yet. Create the first one below.')}</p>
        )}
      </div>

      {/* System-role immutability + active-users banners */}
      {perms.isSystemLocked && (
        <div role="note" className="rounded-xl border border-amber-200 bg-amber-50 p-4 shadow-xs">
          <h3 className="text-sm font-semibold text-amber-900">
            {t('systemLocked.title', 'System role is immutable')}
          </h3>
          <p className="mt-1 text-sm text-amber-800">{t('systemLocked.body', '')}</p>
        </div>
      )}
      {perms.selectedRole &&
        !perms.isSystemLocked &&
        (perms.selectedRole.userCount ?? 0) > 0 && (
          <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 shadow-xs">
            {t('activeUsersWarning', 'Warning: {{count}} active user(s) hold this role.', {
              count: perms.selectedRole.userCount ?? 0,
            })}
          </div>
        )}

      {/* Matrix */}
      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">{t('matrix.title', 'Permission matrix')}</h3>
            <p className="text-xs text-slate-500">{t('matrix.subtitle', '')}</p>
          </div>
          <div className="flex items-center gap-2">
            {perms.dirty && (
              <span className="text-xs font-medium text-amber-700">
                {t('matrix.dirty', 'You have unsaved changes.')}
              </span>
            )}
            <Button
              id="roles-save-btn"
              onClick={() => setConfirmOpen(true)}
              disabled={!perms.dirty || perms.saving || perms.isSystemLocked || perms.matrixLoading}
            >
              {perms.saving ? t('matrix.saving', 'Saving…') : t('matrix.save', 'Save permissions')}
            </Button>
          </div>
        </div>

        {perms.matrixLoading ? (
          <div className="space-y-2" aria-hidden="true">
            {[0, 1, 2, 3].map((i) => (
              <div key={i} className="h-10 animate-pulse rounded-md bg-slate-100" />
            ))}
            <span className="sr-only">{t('matrix.loading', 'Loading permissions…')}</span>
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead scope="col">{t('matrix.colDoctype', 'Document Type')}</TableHead>
                {PERMISSION_ACTIONS.map((action) => {
                  const columnOn =
                    Object.keys(perms.matrix).length > 0 &&
                    Object.values(perms.matrix).every((row) => row[action])
                  return (
                    <TableHead key={action} scope="col" className="text-center">
                      <label className="inline-flex cursor-pointer items-center gap-1.5">
                        <input
                          id={`perm-col-toggle-${action}`}
                          type="checkbox"
                          className="h-4 w-4 accent-sky-600"
                          checked={columnOn}
                          disabled={perms.isSystemLocked}
                          onChange={() => perms.toggleColumn(action)}
                          aria-label={t('matrix.colToggleAria', 'Toggle {{action}} for all document types', {
                            action: actionLabel(action),
                          })}
                        />
                        {actionLabel(action)}
                      </label>
                    </TableHead>
                  )
                })}
              </TableRow>
            </TableHeader>
            <TableBody>
              {DOC_TYPE_MODULES.map((group) => {
                const rows = DOC_TYPES_CATALOG.filter((d) => d.module === group.id)
                if (rows.length === 0) return null
                return [
                  <TableRow key={`group-${group.id}`} className="bg-slate-50">
                    <TableCell colSpan={PERMISSION_ACTIONS.length + 1} className="py-2 text-xs font-bold tracking-wide text-slate-500 uppercase">
                      {groupLabel(group.id)}
                    </TableCell>
                  </TableRow>,
                  ...rows.map((info) => {
                    const flags = perms.matrix[info.docType]
                    const rowOn = flags ? PERMISSION_ACTIONS.every((a) => flags[a]) : false
                    return (
                      <TableRow key={info.docType}>
                        <TableHead scope="row" className="font-medium text-slate-800">
                          <label className="inline-flex cursor-pointer items-center gap-2">
                            <input
                              id={`perm-row-toggle-${info.docType}`}
                              type="checkbox"
                              className="h-4 w-4 accent-sky-600"
                              checked={rowOn}
                              disabled={perms.isSystemLocked}
                              onChange={() => perms.toggleRow(info.docType)}
                              aria-label={t('matrix.rowToggleAria', 'Toggle all permissions for {{doctype}}', {
                                doctype: info.displayName,
                              })}
                            />
                            {info.displayName}
                          </label>
                        </TableHead>
                        {PERMISSION_ACTIONS.map((action) => (
                          <TableCell key={action} className="text-center">
                            <input
                              id={`perm-cell-${info.docType}-${action}`}
                              type="checkbox"
                              className="h-4 w-4 accent-sky-600"
                              checked={flags?.[action] ?? false}
                              disabled={perms.isSystemLocked}
                              onChange={() => perms.toggleCell(info.docType, action)}
                              aria-label={t('matrix.cellAria', '{{action}} permission for {{doctype}}', {
                                action: actionLabel(action),
                                doctype: info.displayName,
                              })}
                            />
                          </TableCell>
                        ))}
                      </TableRow>
                    )
                  }),
                ]
              })}
            </TableBody>
          </Table>
        )}

        {perms.matrixError && (
          <div role="alert" className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
            {translateErrorCode(
              i18n,
              perms.matrixError.code,
              t('matrix.loadFailed', 'Could not load permissions: {{message}}', {
                message: perms.matrixError.message,
              }),
            )}
          </div>
        )}
        {perms.saveError && (
          <div role="alert" className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
            {translateErrorCode(
              i18n,
              perms.saveError.code,
              t('matrix.saveFailed', 'Could not save permissions: {{message}}', {
                message: perms.saveError.message,
              }),
            )}
          </div>
        )}
        {perms.saved && (
          <div className="mt-3 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
            <p className="font-medium">{t('matrix.saved', 'Permissions saved.')}</p>
            <p className="mt-1">{t('nextLoginNotice', '')}</p>
          </div>
        )}
      </div>

      {/* Save confirmation */}
      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('matrix.confirmTitle', 'Save permission matrix?')}</DialogTitle>
            <DialogDescription>
              {t(
                'matrix.confirmBody',
                'This rewrites the permission matrix for role “{{role}}”.',
                { role: perms.selectedRole?.name ?? '' },
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)}>
              {tCommon('action.cancel', 'Cancel')}
            </Button>
            <Button
              id="roles-save-confirm-btn"
              onClick={() => {
                setConfirmOpen(false)
                void perms.save()
              }}
              disabled={perms.saving}
            >
              {t('matrix.confirmOk', 'Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
