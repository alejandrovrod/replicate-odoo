import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { ApiError } from '../../api/client'
import { i18n, loadTestFixtures } from '../../test/i18n-fixtures'
import { UserFormModal } from './UserFormModal'
import { createUserApi, updateUserApi, type RoleDto, type UserAdminDto } from './securityApi'

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
  createUserApi: vi.fn(),
  updateUserApi: vi.fn(),
}))

const create = () => createUserApi as unknown as Mock
const update = () => updateUserApi as unknown as Mock

const ROLES: RoleDto[] = [
  { id: 'r-1', name: 'Administrator', isSystemDefault: true },
  { id: 'r-2', name: 'Accountant', isSystemDefault: false },
]

const USER: UserAdminDto = {
  id: 'u-1',
  email: 'ana@example.com',
  fullName: 'Ana Reyes',
  roleIds: ['r-1'],
  roleNames: ['Administrator'],
  isActive: false,
  createdAt: '2026-01-02T03:04:05Z',
  rowVersion: 'AAAAAA==',
}

const SECURITY_BUNDLE = {
  userForm: {
    createTitle: 'New user',
    editTitle: 'Edit user',
    created: 'User created. Share these credentials securely.',
    fullName: 'Full name',
    email: 'Email',
    temporaryPassword: 'Temporary password (optional)',
    roles: 'Roles',
    active: 'Active account',
    create: 'Create user',
    save: 'Save changes',
    errorFullNameRequired: 'Full name is required',
    errorFullNameTooLong: 'Full name is too long',
    errorEmailInvalid: 'Enter a valid email',
    errorPasswordPolicy: 'The password does not meet the policy',
    errorRolesRequired: 'Pick at least one role',
    lastAdmin: 'At least one active administrator is required',
    saving: 'Saving...',
  },
  users: {
    passwordTitle: 'Temporary password',
    passwordBody: 'Copy it now: it is shown only once.',
    passwordCopy: 'Copy',
    passwordCopied: 'Copied',
  },
  role: { systemBadge: 'System' },
}

describe('UserFormModal (user lifecycle)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'security', SECURITY_BUNDLE, true, true)

    create().mockReset()
    update().mockReset()
  })

  it('blocks submission until the required fields and at least one role are present', async () => {
    const user = userEvent.setup()

    render(
      <UserFormModal open user={null} roles={ROLES} onClose={() => {}} onSaved={() => {}} />,
    )

    expect(screen.getByText('New user')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create user' })).toBeDisabled()
    expect(screen.getByText('Full name is required')).toBeInTheDocument()
    expect(screen.getByText('Enter a valid email')).toBeInTheDocument()
    expect(screen.getByText('Pick at least one role')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Full name'), 'Ana Reyes')
    await user.type(screen.getByLabelText('Email'), 'not-an-email')
    expect(screen.getByRole('button', { name: 'Create user' })).toBeDisabled()

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'ana@example.com')
    await user.click(screen.getByRole('checkbox', { name: /Administrator/ }))

    expect(screen.queryByText('Full name is required')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create user' })).toBeEnabled()
  })

  it('enforces the password policy only when a temporary password is supplied', async () => {
    const user = userEvent.setup()

    render(
      <UserFormModal open user={null} roles={ROLES} onClose={() => {}} onSaved={() => {}} />,
    )

    await user.type(screen.getByLabelText('Full name'), 'Ana Reyes')
    await user.type(screen.getByLabelText('Email'), 'ana@example.com')
    await user.click(screen.getByRole('checkbox', { name: /Administrator/ }))
    await user.type(screen.getByLabelText(/Temporary password/), 'short')

    expect(screen.getByText('The password does not meet the policy')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create user' })).toBeDisabled()
  })

  it('creates the user with trimmed values and shows the one-time password', async () => {
    const user = userEvent.setup()
    const onSaved = vi.fn()
    const onClose = vi.fn()
    create().mockResolvedValue({
      user: { ...USER, isActive: true },
      temporaryPassword: 'Gen3rated!Pass',
    })

    render(<UserFormModal open user={null} roles={ROLES} onClose={onClose} onSaved={onSaved} />)

    await user.type(screen.getByLabelText('Full name'), '  Ana Reyes  ')
    await user.type(screen.getByLabelText('Email'), '  ana@example.com  ')
    await user.click(screen.getByRole('checkbox', { name: /Administrator/ }))
    await user.click(screen.getByRole('checkbox', { name: /Accountant/ }))
    await user.click(screen.getByRole('button', { name: 'Create user' }))

    await waitFor(() => expect(create()).toHaveBeenCalled())
    expect(create()).toHaveBeenCalledWith({
      fullName: 'Ana Reyes',
      email: 'ana@example.com',
      temporaryPassword: null,
      roleIds: ['r-1', 'r-2'],
      isActive: true,
    })
    expect(await screen.findByTestId('temporary-password')).toHaveTextContent('Gen3rated!Pass')
    expect(screen.getByText('User created. Share these credentials securely.')).toBeInTheDocument()
    expect(onSaved).toHaveBeenCalledWith({ ...USER, isActive: true })
    // The dialog stays open so the administrator can hand over the generated password.
    expect(onClose).not.toHaveBeenCalled()
  })

  it('prefills the row, keeps email immutable and updates with the rowversion token', async () => {
    const user = userEvent.setup()
    const onSaved = vi.fn()
    const onClose = vi.fn()
    update().mockResolvedValue({ ...USER, fullName: 'Ana R.', isActive: true })

    render(<UserFormModal open user={USER} roles={ROLES} onClose={onClose} onSaved={onSaved} />)

    expect(screen.getByText('Edit user')).toBeInTheDocument()
    expect(screen.getByLabelText('Full name')).toHaveValue('Ana Reyes')
    expect(screen.getByLabelText('Email')).toHaveValue('ana@example.com')
    expect(screen.getByLabelText('Email')).toBeDisabled()
    expect(screen.queryByLabelText(/Temporary password/)).not.toBeInTheDocument()

    await user.clear(screen.getByLabelText('Full name'))
    await user.type(screen.getByLabelText('Full name'), 'Ana R.')
    await user.click(screen.getByRole('switch', { name: /Active account/ }))
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(update()).toHaveBeenCalled())
    expect(update()).toHaveBeenCalledWith('u-1', {
      fullName: 'Ana R.',
      roleIds: ['r-1'],
      isActive: true,
      rowVersion: 'AAAAAA==',
    })
    expect(onSaved).toHaveBeenCalledWith({ ...USER, fullName: 'Ana R.', isActive: true })
    expect(onClose).toHaveBeenCalled()
  })

  it('surfaces the last-active-administrator invariant without reporting success', async () => {
    const user = userEvent.setup()
    const onSaved = vi.fn()
    update().mockRejectedValue(
      new ApiError(
        409,
        'Conflict',
        'At least one active administrator must remain.',
        'CANNOT_REMOVE_LAST_ADMIN',
      ),
    )

    render(
      <UserFormModal open user={USER} roles={ROLES} onClose={() => {}} onSaved={onSaved} />,
    )

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(
      await screen.findByText('At least one active administrator is required'),
    ).toBeInTheDocument()
    expect(onSaved).not.toHaveBeenCalled()
  })
})
