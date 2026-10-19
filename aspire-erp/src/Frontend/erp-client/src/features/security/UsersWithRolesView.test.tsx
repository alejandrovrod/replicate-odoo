import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeAll, beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { ApiError } from '../../api/client'
import { i18n, loadTestFixtures } from '../../test/i18n-fixtures'
import { UsersWithRolesView } from './UsersWithRolesView'
import {
  getRolesApi,
  getUsersPageApi,
  resetUserPasswordApi,
  setUserActiveApi,
  type PagedUsers,
  type RoleDto,
  type UserAdminDto,
} from './securityApi'

vi.mock('i18next-http-backend', () => ({
  default: class NoopBackend {
    static type = 'backend' as const
    init(): void {}
    read(
      _language: string,
      _namespace: string,
      callback: (err: null, data: Record<string, never>) => void,
    ): void {
      callback(null, {})
    }
  },
}))

vi.mock('./securityApi', () => ({
  getUsersPageApi: vi.fn(),
  getRolesApi: vi.fn(),
  createUserApi: vi.fn(),
  updateUserApi: vi.fn(),
  setUserActiveApi: vi.fn(),
  resetUserPasswordApi: vi.fn(),
}))

const getUsers = () => getUsersPageApi as unknown as Mock
const getRoles = () => getRolesApi as unknown as Mock
const setActive = () => setUserActiveApi as unknown as Mock
const resetPassword = () => resetUserPasswordApi as unknown as Mock

const ROLES: RoleDto[] = [{ id: 'r-1', name: 'Administrator', isSystemDefault: true }]

const ANA: UserAdminDto = {
  id: 'u-1',
  email: 'ana@example.com',
  fullName: 'Ana Reyes',
  roleIds: ['r-1'],
  roleNames: ['Administrator'],
  isActive: true,
  createdAt: '2026-01-02T03:04:05Z',
  rowVersion: 'AAAAAA==',
}

const PAGE: PagedUsers = {
  items: [ANA],
  totalCount: 1,
  pageNumber: 1,
  pageSize: 20,
  totalPages: 1,
}

const EMPTY: PagedUsers = {
  items: [],
  totalCount: 0,
  pageNumber: 1,
  pageSize: 20,
  totalPages: 0,
}

const SECURITY_BUNDLE = {
  users: {
    title: 'Users & roles',
    subtitle: 'Tenant directory',
    search: 'Search users…',
    newUser: 'Nuevo Usuario',
    colUser: 'User',
    colRoles: 'Roles',
    colStatus: 'Status',
    colCreated: 'Created',
    colActions: 'Actions',
    statusActive: 'Active',
    statusInactive: 'Inactive',
    noRoles: 'No roles',
    edit: 'Edit',
    activate: 'Activate',
    deactivate: 'Deactivate',
    resetPassword: 'Reset password',
    resetConfirm: 'Reset password',
    confirmActivateTitle: 'Activate {{name}}?',
    confirmDeactivateTitle: 'Deactivate {{name}}?',
    confirmResetTitle: 'Reset the password of {{name}}?',
    confirmDeactivateBody: 'This user will no longer be able to sign in.',
    confirmResetBody: 'A new temporary password will be generated.',
    working: 'Working…',
    lastAdmin: 'At least one active administrator must remain.',
    loadFailed: 'Could not load users: {{message}}',
    empty: 'No users found.',
    noMatches: 'No users match “{{term}}”.',
    passwordTitle: 'Temporary password',
    passwordBody: 'Copy it now: it is shown only once.',
    passwordCopy: 'Copy',
    passwordCopied: 'Copied',
    passwordResetTitle: 'Password reset',
    passwordResetBody: 'Share this temporary password securely.',
  },
}

beforeAll(() => {
  // Radix menus call pointer-capture APIs that jsdom does not implement.
  Element.prototype.hasPointerCapture = vi.fn()
  Element.prototype.releasePointerCapture = vi.fn()
  Element.prototype.scrollIntoView = vi.fn()
})

describe('UsersWithRolesView (user management)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'security', SECURITY_BUNDLE, true, true)

    getUsers().mockReset()
    getRoles().mockReset()
    setActive().mockReset()
    resetPassword().mockReset()

    getUsers().mockResolvedValue(PAGE)
    getRoles().mockResolvedValue(ROLES)
  })

  it('renders the tenant directory with status, roles and the create action', async () => {
    render(<UsersWithRolesView />)

    expect(await screen.findByText('Ana Reyes')).toBeInTheDocument()
    expect(screen.getByText('ana@example.com')).toBeInTheDocument()
    expect(screen.getByText('Administrator')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Nuevo Usuario/ })).toBeInTheDocument()
    expect(getUsers()).toHaveBeenCalledWith({ page: 1, pageSize: 20, search: '' })
  })

  it('debounces the toolbar search and restarts at page one', async () => {
    const user = userEvent.setup()
    render(<UsersWithRolesView />)

    await screen.findByText('Ana Reyes')

    await user.type(screen.getByRole('textbox', { name: 'Search users…' }), 'ana')

    await waitFor(() =>
      expect(getUsers()).toHaveBeenCalledWith({ page: 1, pageSize: 20, search: 'ana' }),
    )
  })

  it('deactivates a user after confirmation and refreshes the page', async () => {
    const user = userEvent.setup()
    setActive().mockResolvedValue({ ...ANA, isActive: false })
    render(<UsersWithRolesView />)

    await screen.findByText('Ana Reyes')
    await user.click(screen.getByRole('button', { name: 'Actions for Ana Reyes' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Deactivate' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Deactivate Ana Reyes?')).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Deactivate' }))

    await waitFor(() => expect(setActive()).toHaveBeenCalledWith('u-1', false))
    await waitFor(() => expect(getUsers()).toHaveBeenCalledTimes(2))
    expect(await screen.findByText('Active')).toBeInTheDocument()
  })

  it('activates an inactive user from the context menu', async () => {
    const user = userEvent.setup()
    getUsers().mockResolvedValue({ ...PAGE, items: [{ ...ANA, isActive: false }] })
    setActive().mockResolvedValue(ANA)
    render(<UsersWithRolesView />)

    expect(await screen.findByText('Inactive')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Actions for Ana Reyes' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Activate' }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Activate' }))

    await waitFor(() => expect(setActive()).toHaveBeenCalledWith('u-1', true))
  })

  it('keeps the dialog open and explains the last-admin invariant on 409', async () => {
    const user = userEvent.setup()
    setActive().mockRejectedValue(
      new ApiError(
        409,
        'Conflict',
        'At least one active administrator must remain.',
        'CANNOT_REMOVE_LAST_ADMIN',
      ),
    )
    render(<UsersWithRolesView />)

    await screen.findByText('Ana Reyes')
    await user.click(screen.getByRole('button', { name: 'Actions for Ana Reyes' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Deactivate' }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Deactivate' }))

    expect(
      await within(dialog).findByText('At least one active administrator must remain.'),
    ).toBeInTheDocument()
    expect(getUsers()).toHaveBeenCalledTimes(1)
  })

  it('resets a password and shows the one-time credential once', async () => {
    const user = userEvent.setup()
    resetPassword().mockResolvedValue('Res3t!Password')
    render(<UsersWithRolesView />)

    await screen.findByText('Ana Reyes')
    await user.click(screen.getByRole('button', { name: 'Actions for Ana Reyes' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Reset password' }))

    const confirm = await screen.findByRole('dialog')
    expect(within(confirm).getByText('Reset the password of Ana Reyes?')).toBeInTheDocument()

    await user.click(within(confirm).getByRole('button', { name: 'Reset password' }))

    await waitFor(() => expect(resetPassword()).toHaveBeenCalledWith('u-1'))
    expect(await screen.findByTestId('temporary-password')).toHaveTextContent('Res3t!Password')
  })

  it('opens the create dialog from the toolbar', async () => {
    const user = userEvent.setup()
    render(<UsersWithRolesView />)

    await screen.findByText('Ana Reyes')
    await user.click(screen.getByRole('button', { name: /Nuevo Usuario/ }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('New user')).toBeInTheDocument()
    expect(within(dialog).getByRole('checkbox', { name: /Administrator/ })).toBeInTheDocument()
  })

  it('shows an empty-state message when the search finds nothing', async () => {
    const user = userEvent.setup()
    getUsers().mockResolvedValue(EMPTY)
    render(<UsersWithRolesView />)

    expect(await screen.findByText('No users found.')).toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: 'Search users…' }), 'zzz')
    expect(await screen.findByText('No users match “zzz”.')).toBeInTheDocument()
  })
})
