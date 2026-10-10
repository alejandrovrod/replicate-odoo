import { describe, expect, it, beforeEach } from 'vitest'
import { useAuthStore } from './useAuthStore'

/** Minimal unsigned JWT with the given payload (signature never verified client-side). */
function fakeJwt(payload: Record<string, unknown>): string {
  const b64url = (value: unknown): string =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64url({ alg: 'none' })}.${b64url(payload)}.sig`
}

describe('useAuthStore', () => {
  beforeEach(() => {
    useAuthStore.getState().logout()
  })

  it('starts logged out with no permissions', () => {
    const { token, user, hasPermission } = useAuthStore.getState()
    expect(token).toBeNull()
    expect(user).toBeNull()
    expect(hasPermission('customer')).toBe(false)
  })

  it('login parses perms and roles arrays from the token', () => {
    const token = fakeJwt({ perms: ['customer:read', 'customer:write'], roles: ['Sales User'] })
    useAuthStore.getState().login(token, 'Ada', 'ada@example.com')

    const { token: saved, user } = useAuthStore.getState()
    expect(saved).toBe(token)
    expect(user?.email).toBe('ada@example.com')
    expect(user?.perms).toEqual(['customer:read', 'customer:write'])
    expect(user?.roles).toEqual(['Sales User'])
  })

  it('login tolerates a lone string perm claim', () => {
    useAuthStore.getState().login(fakeJwt({ perms: 'customer:read' }), 'Ada', 'a@x.com')
    expect(useAuthStore.getState().user?.perms).toEqual(['customer:read'])
  })

  it('hasPermission matches exact doctype:action pairs', () => {
    useAuthStore.getState().login(fakeJwt({ perms: ['customer:read'], roles: ['Sales User'] }), 'A', 'a@x.com')
    const { hasPermission } = useAuthStore.getState()
    expect(hasPermission('customer', 'read')).toBe(true)
    expect(hasPermission('customer', 'write')).toBe(false)
    expect(hasPermission('supplier', 'read')).toBe(false)
  })

  it('System Manager role bypasses every permission', () => {
    useAuthStore.getState().login(fakeJwt({ perms: [], roles: ['System Manager'] }), 'Root', 'root@x.com')
    const { hasPermission, isSystemManager } = useAuthStore.getState()
    expect(isSystemManager()).toBe(true)
    expect(hasPermission('anything', 'delete')).toBe(true)
  })

  it('isSystemManager is false for functional roles', () => {
    useAuthStore.getState().login(fakeJwt({ roles: ['Sales User'] }), 'S', 's@x.com')
    expect(useAuthStore.getState().isSystemManager()).toBe(false)
  })

  it('logout clears the session', () => {
    useAuthStore.getState().login(fakeJwt({ perms: ['customer:read'] }), 'A', 'a@x.com')
    useAuthStore.getState().logout()
    const { token, user } = useAuthStore.getState()
    expect(token).toBeNull()
    expect(user).toBeNull()
  })

  it('ignores malformed tokens', () => {
    useAuthStore.getState().login('not-a-jwt', 'A', 'a@x.com')
    expect(useAuthStore.getState().user?.perms).toEqual([])
    expect(useAuthStore.getState().user?.roles).toEqual([])
  })
})
