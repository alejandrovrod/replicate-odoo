import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../components/ui/Dialog'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { MAX_PAGE_SIZE } from '../../lib/pagination'
import { useErpAction } from '../../lib/useErpAction'
import { translateErrorCode } from '../../lib/translateErrorCode'
import {
  postEmployee,
  putEmployee,
  useSalaryStructures,
  useStructureAssignments,
} from './useHrPayrollData'
import type { EmployeeInput, EmploymentStatus, HrEmployee, SalaryMode } from './types'

const STATUS_OPTIONS: EmploymentStatus[] = ['Active', 'Inactive', 'Suspended', 'Left']
const SALARY_MODE_OPTIONS: SalaryMode[] = ['Bank', 'Cash', 'Cheque']

// RFC-lite, same shape the domain validator enforces: one `@`, non-empty local/domain, a dot in
// the domain. Kept in sync with EmployeeValidator so the client never sends a doomed payload.
const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/
const MAX_EMPLOYEE_NUMBER = 50
const MAX_NAME = 100
const MAX_EMAIL = 150

/** Editable shape of the employee dialog; exported so a unit test can pin the field rules. */
export interface EmployeeDraft {
  employeeNumber: string
  firstName: string
  lastName: string
  workEmail: string
  dateOfJoining: string
  dateOfRelieving: string
  status: EmploymentStatus
  salaryMode: SalaryMode
  bankName: string
  bankAccountNumber: string
  salaryStructureId: string
}

interface Props {
  /** Non-null opens the dialog; a non-null `employee` switches it to edit mode. */
  open: boolean
  employee: HrEmployee | null
  companyId: string
  onClose: () => void
  /** Fired after the server persisted the employee (create or update). */
  onSaved: (employee: HrEmployee) => void
}

const todayIso = () => new Date().toISOString().slice(0, 10)

function initialDraft(employee: HrEmployee | null): EmployeeDraft {
  if (!employee) {
    return {
      employeeNumber: '',
      firstName: '',
      lastName: '',
      workEmail: '',
      dateOfJoining: todayIso(),
      dateOfRelieving: '',
      status: 'Active',
      salaryMode: 'Bank',
      bankName: '',
      bankAccountNumber: '',
      salaryStructureId: '',
    }
  }

  return {
    employeeNumber: employee.employeeNumber,
    firstName: employee.firstName,
    lastName: employee.lastName,
    workEmail: employee.workEmail,
    dateOfJoining: employee.dateOfJoining,
    dateOfRelieving: employee.dateOfRelieving ?? '',
    status: employee.status,
    salaryMode: employee.salaryMode,
    bankName: employee.bankName ?? '',
    bankAccountNumber: employee.bankAccountNumber ?? '',
    salaryStructureId: '',
  }
}

/** Client mirror of the domain field rules, returned as i18n keys so the UI stays localized. */
export function employeeDraftViolations(draft: EmployeeDraft): string[] {
  const violations: string[] = []

  const number = draft.employeeNumber.trim()
  if (number.length === 0) violations.push('employeeForm.errorNumberRequired')
  else if (number.length > MAX_EMPLOYEE_NUMBER) violations.push('employeeForm.errorNumberTooLong')

  const firstName = draft.firstName.trim()
  if (firstName.length === 0) violations.push('employeeForm.errorFirstNameRequired')
  else if (firstName.length > MAX_NAME) violations.push('employeeForm.errorFirstNameTooLong')

  const lastName = draft.lastName.trim()
  if (lastName.length === 0) violations.push('employeeForm.errorLastNameRequired')
  else if (lastName.length > MAX_NAME) violations.push('employeeForm.errorLastNameTooLong')

  const email = draft.workEmail.trim()
  if (email.length === 0 || email.length > MAX_EMAIL || !EMAIL_PATTERN.test(email)) {
    violations.push('employeeForm.errorEmailInvalid')
  }

  if (draft.dateOfJoining.length === 0) violations.push('employeeForm.errorJoiningRequired')
  if (
    draft.dateOfRelieving.length > 0 &&
    draft.dateOfJoining.length > 0 &&
    draft.dateOfRelieving < draft.dateOfJoining
  ) {
    violations.push('employeeForm.errorRelievingBeforeJoining')
  }

  // Mirrors EmployeeValidator.EnsureValidBankDetails: an ACTIVE bank-paid employee must carry
  // both bank fields. Cash/Cheque and non-active rows are exempt.
  if (
    draft.status === 'Active' &&
    draft.salaryMode === 'Bank' &&
    (draft.bankName.trim().length === 0 || draft.bankAccountNumber.trim().length === 0)
  ) {
    violations.push('employeeForm.errorBankRequired')
  }

  return violations
}

/**
 * Employee registration / edit (spec: HR staff lifecycle). One dialog for both modes: the
 * payload maps to `POST /api/v1/hr/employees` (optionally creating the initial salary-structure
 * assignment) or `PUT /api/v1/hr/employees/{id}` (carrying the rowversion token). RFC 7807
 * rejections - duplicate number 409, stale rowversion 409, validation 400 - surface through
 * `useErpAction`; on success the parent reloads the directory.
 */
export function EmployeeFormModal({ open, employee, companyId, onClose, onSaved }: Props) {
  const { t, i18n } = useTranslation('hr-payroll')
  const [draft, setDraft] = useState<EmployeeDraft>(() => initialDraft(employee))

  const isEdit = employee !== null
  const structuresQuery = useSalaryStructures(companyId, 1, MAX_PAGE_SIZE)
  const assignmentsQuery = useStructureAssignments(companyId, 1, MAX_PAGE_SIZE)

  // Re-seed whenever the dialog opens or targets a different employee.
  useEffect(() => {
    if (open) setDraft(initialDraft(employee))
  }, [open, employee])

  const activeStructures = useMemo(
    () => structuresQuery.items.filter((structure) => structure.isActive),
    [structuresQuery.items],
  )

  const currentAssignment = useMemo(() => {
    if (!employee) return null
    return (
      assignmentsQuery.items.find(
        (assignment) =>
          assignment.employeeId === employee.id &&
          assignment.isActive &&
          assignment.effectiveTo === null,
      ) ?? null
    )
  }, [assignmentsQuery.items, employee])

  const currentStructureName = useMemo(() => {
    if (!currentAssignment) return null
    return (
      structuresQuery.items.find((structure) => structure.id === currentAssignment.structureId)
        ?.structureName ?? null
    )
  }, [currentAssignment, structuresQuery.items])

  const { state, dispatch, isPending } = useErpAction<HrEmployee, EmployeeDraft>(async (payload) => {
    const input: EmployeeInput = {
      employeeNumber: payload.employeeNumber.trim(),
      firstName: payload.firstName.trim(),
      lastName: payload.lastName.trim(),
      workEmail: payload.workEmail.trim(),
      dateOfJoining: payload.dateOfJoining,
      dateOfRelieving: payload.dateOfRelieving.length > 0 ? payload.dateOfRelieving : null,
      salaryMode: payload.salaryMode,
      bankName: payload.bankName.trim().length > 0 ? payload.bankName.trim() : null,
      bankAccountNumber:
        payload.bankAccountNumber.trim().length > 0 ? payload.bankAccountNumber.trim() : null,
      status: payload.status,
    }

    const saved = employee
      ? await putEmployee(employee.id, companyId, employee.rowVersion, input)
      : await postEmployee(companyId, {
          ...input,
          salaryStructureId: payload.salaryStructureId.length > 0 ? payload.salaryStructureId : null,
        })

    onSaved(saved)
    return saved
  })

  const violations = employeeDraftViolations(draft)
  const update = (patch: Partial<EmployeeDraft>) => setDraft((prev) => ({ ...prev, ...patch }))

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) onClose() }}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {isEdit ? t('employeeForm.titleEdit') : t('employeeForm.titleCreate')}
          </DialogTitle>
          <DialogDescription>
            {isEdit ? t('employeeForm.descriptionEdit') : t('employeeForm.descriptionCreate')}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-5">
          <section className="space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t('employeeForm.sectionPersonal')}
            </h4>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label={t('employeeForm.employeeNumber')}>
                <Input
                  value={draft.employeeNumber}
                  onChange={(e) => update({ employeeNumber: e.target.value })}
                  disabled={isPending}
                  maxLength={MAX_EMPLOYEE_NUMBER}
                />
              </Field>
              <Field label={t('employeeForm.workEmail')}>
                <Input
                  type="email"
                  value={draft.workEmail}
                  onChange={(e) => update({ workEmail: e.target.value })}
                  disabled={isPending}
                  maxLength={MAX_EMAIL}
                />
              </Field>
              <Field label={t('employeeForm.firstName')}>
                <Input
                  value={draft.firstName}
                  onChange={(e) => update({ firstName: e.target.value })}
                  disabled={isPending}
                  maxLength={MAX_NAME}
                />
              </Field>
              <Field label={t('employeeForm.lastName')}>
                <Input
                  value={draft.lastName}
                  onChange={(e) => update({ lastName: e.target.value })}
                  disabled={isPending}
                  maxLength={MAX_NAME}
                />
              </Field>
            </div>
          </section>

          <section className="space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t('employeeForm.sectionContract')}
            </h4>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <Field label={t('employeeForm.dateOfJoining')}>
                <Input
                  type="date"
                  value={draft.dateOfJoining}
                  onChange={(e) => update({ dateOfJoining: e.target.value })}
                  disabled={isPending}
                />
              </Field>
              <Field label={t('employeeForm.dateOfRelieving')}>
                <Input
                  type="date"
                  value={draft.dateOfRelieving}
                  onChange={(e) => update({ dateOfRelieving: e.target.value })}
                  disabled={isPending}
                />
              </Field>
              <Field label={t('employeeForm.status')}>
                <Select
                  value={draft.status}
                  onChange={(value) => update({ status: value as EmploymentStatus })}
                  disabled={isPending}
                  options={STATUS_OPTIONS.map((option) => ({ value: option, label: option }))}
                />
              </Field>
            </div>
          </section>

          <section className="space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t('employeeForm.sectionPayment')}
            </h4>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <Field label={t('employeeForm.salaryMode')}>
                <Select
                  value={draft.salaryMode}
                  onChange={(value) => update({ salaryMode: value as SalaryMode })}
                  disabled={isPending}
                  options={SALARY_MODE_OPTIONS.map((option) => ({ value: option, label: option }))}
                />
              </Field>
              <Field label={t('employeeForm.bankName')}>
                <Input
                  value={draft.bankName}
                  onChange={(e) => update({ bankName: e.target.value })}
                  disabled={isPending}
                />
              </Field>
              <Field label={t('employeeForm.bankAccountNumber')}>
                <Input
                  value={draft.bankAccountNumber}
                  onChange={(e) => update({ bankAccountNumber: e.target.value })}
                  disabled={isPending}
                />
              </Field>
            </div>
            <p className="text-[11px] text-slate-400">{t('employeeForm.bankHint')}</p>
          </section>

          <section className="space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t('employeeForm.sectionStructure')}
            </h4>
            {isEdit ? (
              <>
                <Input
                  value={currentStructureName ?? t('employeeForm.salaryStructureNone')}
                  disabled
                  readOnly
                />
                <p className="text-[11px] text-slate-400">
                  {t('employeeForm.salaryStructureEditHint')}
                </p>
              </>
            ) : (
              <Select
                value={draft.salaryStructureId}
                onChange={(value) => update({ salaryStructureId: value })}
                disabled={isPending}
                options={[
                  { value: '', label: t('employeeForm.salaryStructureNone') },
                  ...activeStructures.map((structure) => ({
                    value: structure.id,
                    label: structure.structureName,
                  })),
                ]}
              />
            )}
          </section>
        </div>

        <DialogFooter className="border-t border-slate-100 pt-4">
          {state.error && (
            <p className="mr-auto text-xs text-rose-600">
              {translateErrorCode(i18n, state.errorCode, state.error)}
            </p>
          )}
          {violations.length > 0 && (
            <ul className="mr-auto list-disc pl-4 text-xs text-rose-600">
              {violations.map((key) => (
                <li key={key}>{t(key as 'employeeForm.errorNumberRequired')}</li>
              ))}
            </ul>
          )}
          <Button type="button" variant="outline" onClick={onClose} disabled={isPending}>
            {t('employeeForm.cancel')}
          </Button>
          <Button
            type="button"
            disabled={isPending || violations.length > 0}
            onClick={() => dispatch(draft)}
          >
            {isPending ? t('employeeForm.saving') : t('employeeForm.save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-xs font-medium text-slate-600">
      {label}
      <div className="mt-1">{children}</div>
    </label>
  )
}

function Select({
  value,
  onChange,
  options,
  disabled,
}: {
  value: string
  onChange: (value: string) => void
  options: { value: string; label: string }[]
  disabled?: boolean
}) {
  return (
    <select
      value={value}
      onChange={(event) => onChange(event.target.value)}
      disabled={disabled}
      className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 transition-colors focus-visible:border-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-200 disabled:cursor-not-allowed disabled:opacity-50"
    >
      {options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  )
}
