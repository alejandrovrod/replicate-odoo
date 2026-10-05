/**
 * Strict client mirror of `Erp.Application.DTOs` HR shapes (`EmployeeDto`,
 * `SalaryComponentDto`, `SalaryStructureDto`, `SalaryStructureAssignmentDto`,
 * `PayrollEntryDto`, `SalarySlipDto`, `PayrollSubmitResultDto`,
 * `PayrollEntryDetailDto`). Property names are the camelCase JSON the API emits
 * (enums as their NAME via `JsonStringEnumConverter`, `DateOnly` as "YYYY-MM-DD").
 */

/** Mirrors `EmploymentStatus` (Active / Inactive / Suspended / Left). */
export type EmploymentStatus = 'Active' | 'Inactive' | 'Suspended' | 'Left'

/** Mirrors `SalaryMode` (Bank / Cash / Cheque). */
export type SalaryMode = 'Bank' | 'Cash' | 'Cheque'

/** Mirrors `SalaryComponentType` (Earning / Deduction). */
export type SalaryComponentType = 'Earning' | 'Deduction'

/** Mirrors `PayrollEntryStatus` (Draft -> Submitted -> Paid, Cancelled from Submitted). */
export type PayrollEntryStatus = 'Draft' | 'Submitted' | 'Paid' | 'Cancelled'

/** Mirrors `SalarySlipStatus` (Draft -> Submitted, Cancelled via batch cancel). */
export type SalarySlipStatus = 'Draft' | 'Submitted' | 'Cancelled'

/** Mirrors `EmployeeDto` (GET /api/v1/hr/employees). */
export interface HrEmployee {
  id: string
  companyId: string
  employeeNumber: string
  firstName: string
  lastName: string
  workEmail: string
  departmentId: string | null
  designationId: string | null
  /** DateOnly serializes as "yyyy-MM-dd". */
  dateOfJoining: string
  dateOfRelieving: string | null
  salaryMode: SalaryMode
  status: EmploymentStatus
  isActive: boolean
}

/** Mirrors `SalaryComponentDto` (GET /api/v1/hr/salary-components). */
export interface SalaryComponent {
  id: string
  companyId: string
  componentName: string
  componentType: SalaryComponentType
  dependsOnPaymentDays: boolean
  isTaxApplicable: boolean
  defaultGLAccountId: string
  isActive: boolean
}

/** Mirrors `SalaryStructureLineDto` (nested in GET /api/v1/hr/salary-structures). */
export interface SalaryStructureLine {
  id: string
  componentId: string
  componentName: string
  componentType: SalaryComponentType
  amount: number
  percentageOfBase: number | null
}

/** Mirrors `SalaryStructureDto` (GET /api/v1/hr/salary-structures). */
export interface SalaryStructure {
  id: string
  companyId: string
  structureName: string
  isActive: boolean
  lines: SalaryStructureLine[]
}

/** Mirrors `SalaryStructureAssignmentDto` (GET /api/v1/hr/structure-assignments). */
export interface StructureAssignment {
  id: string
  employeeId: string
  structureId: string
  /** DateOnly serializes as "yyyy-MM-dd". */
  effectiveFrom: string
  effectiveTo: string | null
  isActive: boolean
}

/** Mirrors `SalarySlipLineDto` (nested in the payroll detail read). */
export interface SalarySlipLine {
  id: string
  componentId: string
  componentName: string
  componentType: SalaryComponentType
  amount: number
}

/** Mirrors `SalarySlipDto` (nested in GET /api/v1/payroll-runs/{id}). */
export interface SalarySlip {
  id: string
  employeeId: string
  slipNumber: string
  paymentDays: number
  absentDays: number
  grossPay: number
  totalDeductions: number
  netPay: number
  status: SalarySlipStatus
  lines: SalarySlipLine[]
}

/** Mirrors `PayrollEntryDto` (GET /api/v1/payroll-runs). */
export interface PayrollRun {
  id: string
  companyId: string
  payrollNumber: string
  /** DateOnly serializes as "yyyy-MM-dd". */
  startDate: string
  endDate: string
  postingDate: string
  status: PayrollEntryStatus
  totalGrossPay: number
  totalDeductions: number
  totalNetPay: number
  accrualVoucherNo: string | null
  paymentVoucherNo: string | null
  rowVersion: string
  slipCount: number
}

/** Mirrors `PayrollSkipDto` (one skipped employee of a submit run). */
export interface PayrollSkip {
  employeeId: string
  employeeNumber: string
  reason: string
}

/** Mirrors `PayrollSubmitResultDto` (POST /api/v1/payroll-runs/submit). */
export interface PayrollSubmitResult {
  entry: PayrollRun
  createdSlipCount: number
  skipped: PayrollSkip[]
}

/** Mirrors `PayrollEntryDetailDto` (GET /api/v1/payroll-runs/{id}). */
export interface PayrollRunDetail {
  entry: PayrollRun
  slips: SalarySlip[]
}

/**
 * Client-side HR-03 eligibility (spec invariant, mirrors `Employee.IsEligibleForPeriod`):
 * Status Active, joined on/before period end, not relieved before period start.
 */
export function isEligibleForPeriod(
  employee: HrEmployee,
  periodStart: string,
  periodEnd: string,
): boolean {
  if (employee.status !== 'Active') return false
  if (employee.dateOfJoining > periodEnd) return false
  if (employee.dateOfRelieving !== null && employee.dateOfRelieving < periodStart) return false
  return true
}
