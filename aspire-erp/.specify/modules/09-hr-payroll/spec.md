# Functional Specification: Human Resources & Payroll (ERPNext Parity)

**Module:** `09-hr-payroll`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext HR & Payroll](https://docs.frappe.io/erpnext/hrms)  

---

## 1. Executive Summary & Ubiquitous Language

The **Human Resources & Payroll Module** administers employee profiles, employment terms, organizational hierarchies (departments/designations), recurring **Salary Structures**, leave deductions, and monthly batch **Payroll Processing**. It integrates directly with the **Accounting Module** to generate balanced labor cost accruals and multi-employee bank disbursement payments.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Employee** | `Employee` | Internal staff personnel record holding legal identity, department, designation, date of joining, bank account details, and employment status. |
| **Salary Component** | `Salary Component` | Modular pay element classified as an **Earning** (Basic, Bonus, Transport Allowance) or a **Deduction** (Income Tax, Social Security/Pension, Health Insurance). |
| **Salary Structure** | `Salary Structure` | Master compensation template defining the composition of base earnings and percentage/formulaic deductions for a role. |
| **Salary Structure Assignment** | `Salary Structure Assignment` | Binding contract linking an employee to a specific salary structure from an effective start date. |
| **Payroll Entry** | `Payroll Entry` | Batch transaction orchestrating payroll calculation for all eligible employees across a company or department for a given month/period. |
| **Salary Slip** | `Salary Slip` | Individual pay stub detailing gross earnings, itemized statutory deductions, and net compensation payable to a specific employee. |
| **Payroll Payable** | Interim Liability Account | Balance sheet current liability account holding net salaries owed to staff prior to bank transfer disbursement. |
| **Cost to Company (CTC)** | Total Employer Cost | Sum of gross employee earnings plus employer statutory contributions (payroll taxes, health/pension employer shares). |

---

## 2. Core Business Invariants & Accounting Rules

### Invariant HR-01: Net Pay Mathematical Identity
For every `SalarySlip`:
$$\text{NetPay} = \sum \text{Earnings} - \sum \text{Deductions}$$
- `NetPay` cannot be negative ($\text{NetPay} \ge 0.0000$). If deductions exceed gross earnings, net pay is bounded at zero and surplus deduction is deferred.

### Invariant HR-02: Two-Phase Double-Entry Payroll Accounting

#### Phase 1: Payroll Accrual (`JournalEntry` on Submission)
Recognizes total labor expenses and establishes statutory and salary liabilities:
- **Debit:** `5110 - Salary and Wages Expense` = $\text{TotalGrossPay}$
- **Credit:** `2220 - Income Tax Payable (Statutory)` = $\text{TotalTaxesWithheld}$
- **Credit:** `2225 - Social Security / Pension Payable` = $\text{TotalPensionDeductions}$
- **Credit:** `2150 - Payroll Payable (Net Staff Salaries)` = $\text{TotalNetPay}$
$$\sum \text{Debit} - \sum \text{Credit} == 0.0000$$

#### Phase 2: Payroll Disbursement (`PaymentEntry` on Bank Payout)
Liquidates net salary obligations via bank wire transfer:
- **Debit:** `2150 - Payroll Payable` = $\text{TotalNetPay}$
- **Credit:** `1110 - Bank Account` = $\text{TotalNetPay}$

### Invariant HR-03: Active Employee Payroll Eligibility
Only employees with `Status == Active` whose employment start date is on or before the payroll period end date (`DateOfJoining <= EndDate`) and who have not left prior to the period start date are eligible for salary slips.

---

## 3. Gherkin Functional Scenarios

### Scenario HR-01: Employee Salary Slip Calculation
- **Given** active employee `Maria Santos` with Salary Structure:
  - Basic Salary: $4,000.00 (Earning)
  - Housing Allowance: $1,000.00 (Earning)
  - Income Tax Withholding: $600.00 (Deduction)
  - Pension Contribution: $400.00 (Deduction)
- **When** the monthly payroll batch executes for October 2026
- **Then** a `SalarySlip` is generated with:
  - Gross Pay: $5,000.00
  - Total Deductions: $1,000.00
  - Net Pay: $4,000.00.

### Scenario HR-02: Payroll Batch Accrual Ledger Entry
- **Given** submitted `PayrollEntry` for 20 employees totaling:
  - Gross Pay: $100,000.00
  - Income Taxes Withheld: $12,000.00
  - Pension Deductions: $8,000.00
  - Net Pay: $80,000.00
- **When** the payroll accrual is submitted
- **Then** a balanced `GLEntry` voucher is posted:
  - Debit `5110 - Salaries Expense` for $100,000.00
  - Credit `2220 - Tax Withholding Liability` for $12,000.00
  - Credit `2225 - Pension Payable` for $8,000.00
  - Credit `2150 - Payroll Payable` for $80,000.00
- **And** `PayrollEntry.Status` becomes `Submitted`.

### Scenario HR-03: Bank Disbursement of Net Salaries
- **Given** an accrued `PayrollEntry` with $80,000.00 in `2150 - Payroll Payable`
- **When** the finance officer disburses payroll from `JPMorgan Chase Operating Account`
- **Then** `GLEntry` debits `2150 - Payroll Payable` ($80,000.00) and credits `1110 - Bank Account` ($80,000.00)
- **And** `PayrollPayable` balance associated with this run becomes exactly $0.00
- **And** `PayrollEntry.Status` transitions to `Paid`.
