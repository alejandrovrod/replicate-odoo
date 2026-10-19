import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { EmployeeFormModal } from './EmployeeFormModal'
import { ApiError, apiClient } from '../../api/client'
import { i18n, loadTestFixtures } from '../../test/i18n-fixtures'

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
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))

const get = () => apiClient.get as unknown as Mock
const post = () => apiClient.post as unknown as Mock
const put = () => apiClient.put as unknown as Mock

const EMPTY_PAGE = { items: [], totalCount: 0, pageNumber: 1, pageSize: 500, totalPages: 0 }

const EMPLOYEE = {
  id: 'emp-1',
  companyId: 'c-1',
  employeeNumber: 'EMP-0001',
  firstName: 'Ana',
  lastName: 'Reyes',
  workEmail: 'ana.reyes@example.com',
  departmentId: null,
  designationId: null,
  dateOfJoining: '2025-03-01',
  dateOfRelieving: null,
  salaryMode: 'Bank' as const,
  bankName: 'Acme Bank',
  bankAccountNumber: '000123456789',
  status: 'Active' as const,
  isActive: true,
  rowVersion: 'AAAAAA==',
}

// Only the keys the assertions touch: i18next renders the key itself for anything missing, and
// real key parity is enforced by `i18n-sync --check` + `tsc`, not by this fixture.
const HR_BUNDLE = {
  employeeForm: {
    titleCreate: 'New employee',
    titleEdit: 'Edit employee',
    employeeNumber: 'Employee number',
    firstName: 'First name',
    lastName: 'Last name',
    workEmail: 'Work email',
    bankName: 'Bank name',
    bankAccountNumber: 'Bank account number',
    salaryMode: 'Salary mode',
    save: 'Save',
    errorBankRequired:
      'Bank name and account number are required for a bank-paid active employee.',
  },
}

describe('EmployeeFormModal (HR staff lifecycle)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'hr-payroll', HR_BUNDLE, true, true)
    i18n.addResourceBundle(
      'en',
      'error',
      { duplicate_employee_number: 'An employee with this number already exists.' },
      true,
      true,
    )

    get().mockReset()
    post().mockReset()
    put().mockReset()
    get().mockResolvedValue({ data: EMPTY_PAGE })
  })

  it('blocks submission until the required fields and the bank data are present', async () => {
    const user = userEvent.setup()

    render(
      <EmployeeFormModal open employee={null} companyId="c-1" onClose={() => {}} onSaved={() => {}} />,
    )

    expect(screen.getByText('New employee')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    expect(
      screen.getByText('Bank name and account number are required for a bank-paid active employee.'),
    ).toBeInTheDocument()

    await user.type(screen.getByLabelText('Employee number'), 'EMP-0002')
    await user.type(screen.getByLabelText('First name'), 'Ana')
    await user.type(screen.getByLabelText('Last name'), 'Reyes')
    await user.type(screen.getByLabelText('Work email'), 'ana@example.com')
    await user.type(screen.getByLabelText('Bank name'), 'Acme Bank')
    await user.type(screen.getByLabelText('Bank account number'), '000123456789')

    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()
  })

  it('registers an employee with the trimmed payload', async () => {
    const user = userEvent.setup()
    const onSaved = vi.fn()
    post().mockResolvedValue({ data: { id: 'emp-9' } })

    render(
      <EmployeeFormModal
        open
        employee={null}
        companyId="c-1"
        onClose={() => {}}
        onSaved={onSaved}
      />,
    )

    await user.type(screen.getByLabelText('Employee number'), ' EMP-0002 ')
    await user.type(screen.getByLabelText('First name'), 'Ana')
    await user.type(screen.getByLabelText('Last name'), 'Reyes')
    await user.type(screen.getByLabelText('Work email'), 'ana@example.com')
    await user.type(screen.getByLabelText('Bank name'), 'Acme Bank')
    await user.type(screen.getByLabelText('Bank account number'), '000123456789')

    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(post()).toHaveBeenCalled())
    const [url, body] = post().mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/v1/hr/employees')
    expect(body).toMatchObject({
      companyId: 'c-1',
      employeeNumber: 'EMP-0002',
      firstName: 'Ana',
      lastName: 'Reyes',
      workEmail: 'ana@example.com',
      salaryMode: 'Bank',
      bankName: 'Acme Bank',
      bankAccountNumber: '000123456789',
      status: 'Active',
    })
    expect(onSaved).toHaveBeenCalledWith({ id: 'emp-9' })
  })

  it('prefills the row and updates it with the rowversion token', async () => {
    const user = userEvent.setup()
    const onSaved = vi.fn()
    put().mockResolvedValue({ data: EMPLOYEE })

    render(
      <EmployeeFormModal
        open
        employee={EMPLOYEE}
        companyId="c-1"
        onClose={() => {}}
        onSaved={onSaved}
      />,
    )

    expect(screen.getByText('Edit employee')).toBeInTheDocument()
    expect(screen.getByLabelText('Employee number')).toHaveValue('EMP-0001')
    expect(screen.getByLabelText('Work email')).toHaveValue('ana.reyes@example.com')

    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(put()).toHaveBeenCalled())
    const [url, body] = put().mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/v1/hr/employees/emp-1')
    expect(body).toMatchObject({
      companyId: 'c-1',
      rowVersion: 'AAAAAA==',
      employeeNumber: 'EMP-0001',
    })
    expect(onSaved).toHaveBeenCalledWith(EMPLOYEE)
  })

  it('surfaces a duplicate-number rejection and never reports success', async () => {
    const user = userEvent.setup()
    const onSaved = vi.fn()
    post().mockRejectedValue(
      new ApiError(
        409,
        'Conflict',
        'An employee with this number already exists.',
        'duplicate_employee_number',
      ),
    )

    render(
      <EmployeeFormModal
        open
        employee={null}
        companyId="c-1"
        onClose={() => {}}
        onSaved={onSaved}
      />,
    )

    await user.type(screen.getByLabelText('Employee number'), 'EMP-0001')
    await user.type(screen.getByLabelText('First name'), 'Ana')
    await user.type(screen.getByLabelText('Last name'), 'Reyes')
    await user.type(screen.getByLabelText('Work email'), 'ana@example.com')
    await user.type(screen.getByLabelText('Bank name'), 'Acme Bank')
    await user.type(screen.getByLabelText('Bank account number'), '000123456789')

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('An employee with this number already exists.'),
    ).toBeInTheDocument()
    expect(onSaved).not.toHaveBeenCalled()
  })
})
