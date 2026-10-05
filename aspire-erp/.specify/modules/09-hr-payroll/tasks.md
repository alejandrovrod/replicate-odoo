# Implementation Tasks: Human Resources & Payroll (ERPNext Parity)

**Module:** `09-hr-payroll`  
**Specification:** [spec.md](./spec.md)  
**Technical Plan:** [plan.md](./plan.md)  
**Status:** IN PROGRESS (Block A: 12.1, 12.2 complete)  

---

## Phase 12: Human Resources & Batch Payroll

- [x] **Task 12.1: Employee Directory & Organizational Structure**
  - **Action:** Implement `Employee`, `Department`, and `Designation` entities in `Erp.Domain.HR` with date of joining and banking details.
  - **Acceptance:** Validates that active employees have valid email and bank accounts.

- [x] **Task 12.2: Salary Component & Structure Configuration**
  - **Action:** Create `SalaryComponent` (Earnings, Deductions) and `SalaryStructure` linking components to GL accounts.
  - **Acceptance:** Ensures all components map to valid leaf posting accounts in the Chart of Accounts.

- [ ] **Task 12.3: Monthly Payroll Batch Engine (`PayrollEntry`)**
  - **Action:** Implement `PayrollEntry` generating individual `SalarySlip` pay stubs for all active eligible employees for a given month.
  - **Acceptance:** Invariant verified: $\text{GrossPay} - \text{TotalDeductions} == \text{NetPay} \ge 0.00$.

- [ ] **Task 12.4: Two-Phase Payroll Accounting Integration**
  - **Action:** Phase 1 posts balanced labor cost accrual to `GLEntry`; Phase 2 executes bank wire transfer payment clearing payroll payable.
  - **Acceptance:** `PayrollPayable` balance associated with the completed run returns to exactly $0.00.

- [ ] **Task 12.5: React HR Directory & Payroll Studio UI**
  - **Action:** Build employee directory list and monthly payroll run processing workbench in `erp-client`.
  - **Acceptance:** Real-time visibility into net pay disbursements and pay stub details.
