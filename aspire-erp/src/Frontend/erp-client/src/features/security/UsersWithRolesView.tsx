/**
 * User Management (user-management spec D2/D4): tenant-scoped user directory with server-side
 * search + pagination, a floating `+ Nuevo Usuario` action, and a per-row context menu
 * (Edit, Activate/Deactivate, Reset password).
 *
 * Authorization is enforced server-side (`permission:user:read|create|write`); this view only
 * hides impossible actions and surfaces RFC 7807 codes such as `CANNOT_REMOVE_LAST_ADMIN` (409)
 * and `STALE_ROW_VERSION` (409) as semantic messages.
 */
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { MoreHorizontal } from 'lucide-react'
import { ApiError } from '../../api/client'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { Button } from '../../components/ui/Button'
import { FloatingActionButton } from '../../components/ui/FloatingActionButton'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../components/ui/Dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../../components/ui/DropdownMenu'
import { Input } from '../../components/ui/Input'
import { Pagination } from '../../components/ui/Pagination'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/Table'
import {
  getRolesApi,
  getUsersPageApi,
  resetUserPasswordApi,
  setUserActiveApi,
  type PagedUsers,
  type RoleDto,
  type UserAdminDto,
} from './securityApi'
import { TemporaryPasswordPanel } from './TemporaryPasswordPanel'
import { UserFormModal } from './UserFormModal'

type Confirmation = { kind: 'activate' | 'deactivate' | 'reset'; user: UserAdminDto }

const EMPTY_PAGE: PagedUsers = {
  items: [],
  totalCount: 0,
  pageNumber: 1,
  pageSize: 20,
  totalPages: 0,
}

const PAGE_SIZE = 20

export function UsersWithRolesView() {
  const { t, i18n } = useTranslation('security')
  const { t: tCommon } = useTranslation('common')

  const [pageState, setPageState] = useState<PagedUsers>(EMPTY_PAGE)
  const [roles, setRoles] = useState<RoleDto[]>([])
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<ApiError | null>(null)
  const [refreshToken, setRefreshToken] = useState(0)

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<UserAdminDto | null>(null)
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<ApiError | null>(null)
  const [temporaryPassword, setTemporaryPassword] = useState<string | null>(null)

  // Server-side search is debounced; a new term always restarts at page 1.
  useEffect(() => {
    const timer = setTimeout(() => {
      const term = search.trim()
      setDebouncedSearch((current) => (current === term ? current : term))
      setPage(1)
    }, 300)
    return () => clearTimeout(timer)
  }, [search])

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)

    Promise.all([
      getUsersPageApi({ page, pageSize: PAGE_SIZE, search: debouncedSearch }),
      getRolesApi(),
    ]).then(
      ([users, roleList]) => {
        if (cancelled) return
        setPageState(users)
        setRoles(roleList)
        setLoading(false)
      },
      (cause: unknown) => {
        if (cancelled) return
        setPageState(EMPTY_PAGE)
        setError(cause instanceof ApiError ? cause : new ApiError(0, 'Load failed', String(cause)))
        setLoading(false)
      },
    )

    return () => {
      cancelled = true
    }
  }, [page, debouncedSearch, refreshToken])

  const refresh = () => setRefreshToken((n) => n + 1)

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (user: UserAdminDto) => {
    setEditing(user)
    setFormOpen(true)
  }

  const runConfirmation = async () => {
    if (!confirmation) return
    setBusy(true)
    setActionError(null)

    try {
      if (confirmation.kind === 'reset') {
        const password = await resetUserPasswordApi(confirmation.user.id)
        setTemporaryPassword(password)
      } else {
        await setUserActiveApi(confirmation.user.id, confirmation.kind === 'activate')
        refresh()
      }
      setConfirmation(null)
    } catch (cause) {
      setActionError(cause instanceof ApiError ? cause : new ApiError(0, 'Action failed', String(cause)))
    } finally {
      setBusy(false)
    }
  }

  const confirmationCopy = (entry: Confirmation) => {
    switch (entry.kind) {
      case 'deactivate':
        return {
          title: t('users.confirmDeactivateTitle', 'Deactivate {{name}}?', {
            name: entry.user.fullName,
          }),
          body: t(
            'users.confirmDeactivateBody',
            'This user will no longer be able to sign in. Their data and role assignments are kept.',
          ),
          ok: t('users.deactivate', 'Deactivate'),
        }
      case 'activate':
        return {
          title: t('users.confirmActivateTitle', 'Activate {{name}}?', {
            name: entry.user.fullName,
          }),
          body: t('users.confirmActivateBody', 'This user will be able to sign in again.'),
          ok: t('users.activate', 'Activate'),
        }
      default:
        return {
          title: t('users.confirmResetTitle', 'Reset the password of {{name}}?', {
            name: entry.user.fullName,
          }),
          body: t(
            'users.confirmResetBody',
            'A new temporary password will be generated and shown once. The current password stops working immediately.',
          ),
          ok: t('users.resetConfirm', 'Reset password'),
        }
    }
  }

  const copy = confirmation ? confirmationCopy(confirmation) : null
  const lastAdmin = actionError?.code === 'CANNOT_REMOVE_LAST_ADMIN'
  const stale = actionError?.code === 'STALE_ROW_VERSION'

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold text-slate-900">{t('users.title', 'Users & roles')}</h3>
          <p className="text-xs text-slate-500">{t('users.subtitle', '')}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Input
            id="user-search"
            className="h-9 w-64"
            placeholder={t('users.search', 'Search users…')}
            aria-label={t('users.search', 'Search users…')}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
      </div>

      <FloatingActionButton
        id="user-create-open"
        label={t('users.newUser', 'Nuevo Usuario')}
        onClick={openCreate}
      />

      {loading ? (
        <div className="space-y-2" aria-hidden="true">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-10 animate-pulse rounded-md bg-slate-100" />
          ))}
          <span className="sr-only">{t('users.loading', 'Loading users…')}</span>
        </div>
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead scope="col">{t('users.colUser', 'User')}</TableHead>
                <TableHead scope="col">{t('users.colRoles', 'Roles')}</TableHead>
                <TableHead scope="col">{t('users.colStatus', 'Status')}</TableHead>
                <TableHead scope="col">{t('users.colCreated', 'Created')}</TableHead>
                <TableHead scope="col" className="text-right">
                  {t('users.colActions', 'Actions')}
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {pageState.items.map((user) => (
                <TableRow key={user.id}>
                  <TableCell>
                    <div className="font-medium text-slate-900">{user.fullName}</div>
                    <div className="text-xs text-slate-500">{user.email}</div>
                  </TableCell>
                  <TableCell>
                    {user.roleNames.length === 0 ? (
                      <span className="text-xs text-slate-400">{t('users.noRoles', 'No roles')}</span>
                    ) : (
                      <div className="flex flex-wrap gap-1">
                        {user.roleNames.map((name) => (
                          <span
                            key={name}
                            className="rounded bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-700"
                          >
                            {name}
                          </span>
                        ))}
                      </div>
                    )}
                  </TableCell>
                  <TableCell>
                    <span
                      className={
                        user.isActive
                          ? 'inline-flex rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700'
                          : 'inline-flex rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600'
                      }
                    >
                      {user.isActive
                        ? t('users.statusActive', 'Active')
                        : t('users.statusInactive', 'Inactive')}
                    </span>
                  </TableCell>
                  <TableCell className="text-xs text-slate-500">
                    {formatDate(user.createdAt, i18n.language)}
                  </TableCell>
                  <TableCell className="text-right">
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button
                          id={`user-actions-${user.id}`}
                          variant="ghost"
                          size="icon"
                          aria-label={t('users.actionsFor', 'Actions for {{name}}', {
                            name: user.fullName,
                          })}
                        >
                          <MoreHorizontal className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onSelect={() => openEdit(user)}>
                          {t('users.edit', 'Edit')}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onSelect={() =>
                            setConfirmation({
                              kind: user.isActive ? 'deactivate' : 'activate',
                              user,
                            })
                          }
                        >
                          {user.isActive
                            ? t('users.deactivate', 'Deactivate')
                            : t('users.activate', 'Activate')}
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem
                          onSelect={() => setConfirmation({ kind: 'reset', user })}
                        >
                          {t('users.resetPassword', 'Reset password')}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {pageState.items.length === 0 && !error && (
            <p className="mt-3 text-sm text-slate-500">
              {debouncedSearch
                ? t('users.noMatches', 'No users match “{{term}}”.', { term: debouncedSearch })
                : t('users.empty', 'No users found.')}
            </p>
          )}

          {pageState.totalCount > 0 && (
            <div className="mt-4 flex justify-end">
              <Pagination
                totalCount={pageState.totalCount}
                page={pageState.pageNumber}
                pageSize={pageState.pageSize}
                onPageChange={setPage}
              />
            </div>
          )}
        </>
      )}

      {error && (
        <div
          role="alert"
          className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800"
        >
          {translateErrorCode(
            i18n,
            error.code,
            t('users.loadFailed', 'Could not load users: {{message}}', {
              message: error.message,
            }),
          )}
        </div>
      )}

      <UserFormModal
        open={formOpen}
        user={editing}
        roles={roles}
        onClose={() => setFormOpen(false)}
        onSaved={() => refresh()}
      />

      <Dialog
        open={confirmation !== null}
        onOpenChange={(next) => {
          if (!next) {
            setConfirmation(null)
            setActionError(null)
          }
        }}
      >
        <DialogContent>
          {copy && (
            <>
              <DialogHeader>
                <DialogTitle>{copy.title}</DialogTitle>
                <DialogDescription>{copy.body}</DialogDescription>
              </DialogHeader>

              {lastAdmin ? (
                <div
                  role="alert"
                  className="rounded-md border border-red-300 bg-red-50 p-3 text-sm font-medium text-red-800"
                >
                  {t('users.lastAdmin', '')}
                </div>
              ) : stale ? (
                <div
                  role="alert"
                  className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm font-medium text-amber-900"
                >
                  {t(
                    'users.staleRow',
                    'This user changed in another session. Close and reload the list.',
                  )}
                </div>
              ) : (
                actionError && (
                  <div
                    role="alert"
                    className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800"
                  >
                    {translateErrorCode(i18n, actionError.code, actionError.message)}
                  </div>
                )
              )}

              <DialogFooter>
                <Button
                  variant="outline"
                  onClick={() => {
                    setConfirmation(null)
                    setActionError(null)
                  }}
                  disabled={busy}
                >
                  {tCommon('action.cancel', 'Cancel')}
                </Button>
                <Button onClick={() => void runConfirmation()} disabled={busy}>
                  {busy ? t('users.working', 'Working…') : copy.ok}
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      <Dialog
        open={temporaryPassword !== null}
        onOpenChange={(next) => {
          if (!next) setTemporaryPassword(null)
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('users.passwordResetTitle', 'Password reset')}</DialogTitle>
            <DialogDescription>
              {t(
                'users.passwordResetBody',
                'Share this temporary password with the user through a secure channel.',
              )}
            </DialogDescription>
          </DialogHeader>
          {temporaryPassword && <TemporaryPasswordPanel password={temporaryPassword} />}
          <DialogFooter>
            <Button onClick={() => setTemporaryPassword(null)}>
              {tCommon('action.close', 'Close')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function formatDate(value: string, language: string): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat(language, { dateStyle: 'medium' }).format(date)
}
