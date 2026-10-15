/**
 * Tenant users with their role assignments. Each row opens `UserRolesModal`
 * (`user-roles-open-{userId}`) to tick/untick roles.
 *
 * Tolerates the parallel Fase 1 backend: a missing users collection renders the
 * empty state instead of blocking the whole Security section.
 */
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { Button } from '../../components/ui/Button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/Table'
import { getRolesApi, getUsersWithRolesApi, type RoleDto, type UserWithRolesDto } from './securityApi'
import { UserRolesModal } from './UserRolesModal'

export function UsersWithRolesView() {
  const { t, i18n } = useTranslation('security')
  const [users, setUsers] = useState<UserWithRolesDto[]>([])
  const [roles, setRoles] = useState<RoleDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<ApiError | null>(null)
  const [search, setSearch] = useState('')
  const [activeUser, setActiveUser] = useState<UserWithRolesDto | null>(null)
  const [refreshToken, setRefreshToken] = useState(0)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    Promise.all([getUsersWithRolesApi(), getRolesApi()]).then(
      ([u, r]) => {
        if (cancelled) return
        setUsers(u)
        setRoles(r)
        setLoading(false)
      },
      (cause: unknown) => {
        if (cancelled) return
        setUsers([])
        setRoles([])
        setError(
          cause instanceof ApiError ? cause : new ApiError(0, 'Load failed', String(cause)),
        )
        setLoading(false)
      },
    )
    return () => {
      cancelled = true
    }
  }, [refreshToken])

  const roleNameById = useMemo(() => {
    const map = new Map<string, string>()
    for (const role of roles) map.set(role.id, role.name)
    return map
  }, [roles])

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return users
    return users.filter(
      (u) => u.email.toLowerCase().includes(q) || u.fullName.toLowerCase().includes(q),
    )
  }, [users, search])

  const namesFor = (user: UserWithRolesDto): string[] => {
    const fromIds = user.roleIds
      .map((id) => roleNameById.get(id))
      .filter((n): n is string => Boolean(n))
    const merged = [...user.roleNames, ...fromIds]
    return [...new Set(merged)]
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold text-slate-900">{t('users.title', 'Users & roles')}</h3>
          <p className="text-xs text-slate-500">{t('users.subtitle', '')}</p>
        </div>
        <input
          className="h-9 rounded-md border border-slate-200 px-3 text-sm focus:ring-2 focus:ring-sky-600 focus:outline-none"
          placeholder={t('users.search', 'Search users…')}
          aria-label={t('users.search', 'Search users…')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {loading ? (
        <div className="space-y-2" aria-hidden="true">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-10 animate-pulse rounded-md bg-slate-100" />
          ))}
          <span className="sr-only">{t('users.loading', 'Loading users…')}</span>
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">{t('users.colUser', 'User')}</TableHead>
              <TableHead scope="col">{t('users.colRoles', 'Roles')}</TableHead>
              <TableHead scope="col" className="text-right">
                {t('users.colActions', 'Actions')}
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.map((user) => {
              const names = namesFor(user)
              return (
                <TableRow key={user.id}>
                  <TableCell>
                    <div className="font-medium text-slate-900">{user.fullName}</div>
                    <div className="text-xs text-slate-500">{user.email}</div>
                  </TableCell>
                  <TableCell>
                    {names.length === 0 ? (
                      <span className="text-xs text-slate-400">{t('users.noRoles', 'No roles')}</span>
                    ) : (
                      <span className="text-sm text-slate-700">{names.join(', ')}</span>
                    )}
                  </TableCell>
                  <TableCell className="text-right">
                    <Button
                      id={`user-roles-open-${user.id}`}
                      size="sm"
                      variant="outline"
                      onClick={() => setActiveUser(user)}
                    >
                      {t('users.manageRoles', 'Manage roles')}
                    </Button>
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      )}

      {!loading && !error && filtered.length === 0 && (
        <p className="mt-3 text-sm text-slate-500">{t('users.empty', 'No users found.')}</p>
      )}
      {error && (
        <div role="alert" className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
          {translateErrorCode(
            i18n,
            error.code,
            t('users.loadFailed', 'Could not load users: {{message}}', { message: error.message }),
          )}
        </div>
      )}

      <UserRolesModal
        user={activeUser}
        roles={roles}
        onClose={() => setActiveUser(null)}
        onSaved={() => setRefreshToken((n) => n + 1)}
      />
    </div>
  )
}
