import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { apiClient } from '../../api/client'
import {
  createUserApi,
  getUserApi,
  getUsersPageApi,
  resetUserPasswordApi,
  setUserActiveApi,
  updateUserApi,
} from './securityApi'

vi.mock('../../api/client', () => ({
  ApiError: class MockApiError extends Error {
    status: number
    code?: string
    constructor(status: number, title: string, detail: string, code?: string) {
      super(detail || title)
      this.status = status
      this.code = code
    }
  },
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
}))

const get = () => apiClient.get as unknown as Mock
const post = () => apiClient.post as unknown as Mock
const put = () => apiClient.put as unknown as Mock
const patch = () => apiClient.patch as unknown as Mock

const USER_ROW = {
  id: 'u-1',
  email: 'ana@example.com',
  fullName: 'Ana Reyes',
  roleIds: ['r-1'],
  roleNames: ['Administrator'],
  isActive: false,
  createdAt: '2026-01-02T03:04:05Z',
  rowVersion: 'AAAAAA==',
}

describe('securityApi user management wire contract', () => {
  beforeEach(() => {
    get().mockReset()
    post().mockReset()
    put().mockReset()
    patch().mockReset()
  })

  it('requests a tenant page with a trimmed search term and normalizes the envelope', async () => {
    get().mockResolvedValue({
      data: {
        items: [USER_ROW, { email: 'broken@example.com' }],
        totalCount: 41,
        pageNumber: 2,
        pageSize: 20,
        totalPages: 3,
      },
    })

    const page = await getUsersPageApi({ page: 2, pageSize: 20, search: '  ana  ' })

    expect(get()).toHaveBeenCalledWith('/v1/users', {
      params: { page: 2, pageSize: 20, search: 'ana' },
    })
    expect(page.totalCount).toBe(41)
    expect(page.totalPages).toBe(3)
    expect(page.items).toHaveLength(1)
    expect(page.items[0]).toEqual({
      id: 'u-1',
      email: 'ana@example.com',
      fullName: 'Ana Reyes',
      roleIds: ['r-1'],
      roleNames: ['Administrator'],
      isActive: false,
      createdAt: '2026-01-02T03:04:05Z',
      rowVersion: 'AAAAAA==',
    })
  })

  it('omits a blank search and derives paging defaults', async () => {
    get().mockResolvedValue({
      data: {
        items: [{ ...USER_ROW, isActive: undefined, rowVersion: undefined }],
        totalCount: 41,
        pageSize: 20,
      },
    })

    const page = await getUsersPageApi({ search: '   ' })

    expect(get()).toHaveBeenCalledWith('/v1/users', {
      params: { page: 1, pageSize: 20, search: undefined },
    })
    expect(page.pageNumber).toBe(1)
    expect(page.totalPages).toBe(3)
    expect(page.items[0].isActive).toBe(true)
    expect(page.items[0].rowVersion).toBeNull()
  })

  it('creates a user and returns the one-time password from the server', async () => {
    post().mockResolvedValue({ data: { user: USER_ROW, temporaryPassword: 'Gen3rated!Pass' } })

    const created = await createUserApi({
      fullName: 'Ana Reyes',
      email: 'ana@example.com',
      temporaryPassword: null,
      roleIds: ['r-1'],
      isActive: true,
    })

    expect(post()).toHaveBeenCalledWith('/v1/users', {
      fullName: 'Ana Reyes',
      email: 'ana@example.com',
      temporaryPassword: null,
      roleIds: ['r-1'],
      isActive: true,
    })
    expect(created.temporaryPassword).toBe('Gen3rated!Pass')
    expect(created.user.id).toBe('u-1')
  })

  it('falls back to the supplied password when the server omits it', async () => {
    post().mockResolvedValue({ data: { user: USER_ROW } })

    const created = await createUserApi({
      fullName: 'Ana Reyes',
      email: 'ana@example.com',
      temporaryPassword: 'Supplied1Pass',
      roleIds: ['r-1'],
      isActive: true,
    })

    expect(created.temporaryPassword).toBe('Supplied1Pass')
  })

  it('sends the rowversion token on update and preserves null status', async () => {
    put().mockResolvedValue({ data: { ...USER_ROW, fullName: 'Ana R.' } })

    const saved = await updateUserApi('u-1', {
      fullName: 'Ana R.',
      roleIds: ['r-1'],
      isActive: null,
      rowVersion: 'AAAAAA==',
    })

    expect(put()).toHaveBeenCalledWith('/v1/users/u-1', {
      fullName: 'Ana R.',
      roleIds: ['r-1'],
      isActive: null,
      rowVersion: 'AAAAAA==',
    })
    expect(saved.fullName).toBe('Ana R.')
  })

  it('toggles the lifecycle flag through the status sub-resource', async () => {
    patch().mockResolvedValue({ data: { ...USER_ROW, isActive: true } })

    const saved = await setUserActiveApi('u-1', true)

    expect(patch()).toHaveBeenCalledWith('/v1/users/u-1/status', { isActive: true })
    expect(saved.isActive).toBe(true)
  })

  it('returns the generated password on reset and rejects an empty response', async () => {
    post().mockResolvedValueOnce({ data: { temporaryPassword: 'Reset3d!Pass' } })
    await expect(resetUserPasswordApi('u-1')).resolves.toBe('Reset3d!Pass')
    expect(post()).toHaveBeenCalledWith('/v1/users/u-1/reset-password', {
      temporaryPassword: null,
    })

    post().mockResolvedValueOnce({ data: {} })
    await expect(resetUserPasswordApi('u-1')).rejects.toThrow('no temporary password')
  })

  it('rejects an unparsable single-user payload', async () => {
    get().mockResolvedValue({ data: { email: 'nobody@example.com' } })

    await expect(getUserApi('u-1')).rejects.toThrow('unparsable user')
  })
})
