import React from 'react'
import { useAuthStore } from '../../store/useAuthStore'

interface RequirePermissionProps {
  resource: string
  action?: string
  children: React.ReactNode
  fallback?: React.ReactNode
}

/**
 * Hides or replaces UI elements if the user does not have the required permission.
 * 
 * @example
 * <RequirePermission resource="project" action="write" fallback={<button disabled>New Project</button>}>
 *   <button>New Project</button>
 * </RequirePermission>
 */
export function RequirePermission({
  resource,
  action = 'read',
  children,
  fallback = null,
}: RequirePermissionProps) {
  const hasPermission = useAuthStore((state) => state.hasPermission(resource, action))

  if (!hasPermission) {
    return <>{fallback}</>
  }

  return <>{children}</>
}
