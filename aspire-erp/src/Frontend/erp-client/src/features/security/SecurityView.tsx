/**
 * Security section entry point: permission matrix + user assignments.
 *
 * Rendered only for System Manager (menu gate in `Sidebar`, route gate in
 * `App.tsx`) — UX concealment only. The real authorization stays server-side
 * (`TenantMember` policy + `permission:` policies + system guards, SEC-G4).
 */
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { RolePermissionMatrixView } from './RolePermissionMatrixView'
import { UsersWithRolesView } from './UsersWithRolesView'

type SecurityTab = 'permissions' | 'users'

export function SecurityView() {
  const { t } = useTranslation('security')
  const [tab, setTab] = useState<SecurityTab>('permissions')

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          {t('page.title', 'Roles & Permissions')}
        </h2>
        <p className="text-xs text-slate-500">{t('page.subtitle', '')}</p>
      </div>

      <div className="flex gap-1 rounded-lg border border-slate-200 bg-white p-1 shadow-xs" role="tablist">
        {(['permissions', 'users'] as const).map((id) => (
          <button
            key={id}
            type="button"
            role="tab"
            aria-selected={tab === id}
            onClick={() => setTab(id)}
            className={`flex-1 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
              tab === id ? 'bg-sky-50 text-sky-700 shadow-xs' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            {t(`tabs.${id}`, id)}
          </button>
        ))}
      </div>

      {tab === 'permissions' ? <RolePermissionMatrixView /> : <UsersWithRolesView />}
    </div>
  )
}
