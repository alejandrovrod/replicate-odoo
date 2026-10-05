namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the HR &amp; Payroll module (Tasks 12.1-12.2). They
/// flow Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controller maps them to
/// RFC 7807 status codes, mirroring <see cref="StockErrorCodes"/> and
/// <see cref="AssetErrorCodes"/>.
/// </summary>
/// <remarks>
/// Deliberate deviation from plan.md: the plan text uses SCREAMING codes under a dedicated
/// <c>Erp.Domain.HR</c> namespace; this file follows the repo convention (snake_case codes in
/// <c>Erp.Domain.Entities</c>, exceptions in <c>Erp.Domain.Exceptions</c>) like every other
/// module.
/// </remarks>
public static class HrPayrollErrorCodes
{
    // Department (Task 12.1)
    public const string DepartmentCompanyRequired = "department_company_required";
    public const string DepartmentNameRequired = "department_name_required";
    public const string DepartmentNameTooLong = "department_name_too_long";
    public const string ParentIsSelf = "parent_is_self";
    public const string ParentNotInSameCompany = "parent_not_in_same_company";
    public const string CycleDetected = "cycle_detected";
    public const string ParentDepartmentNotFound = "parent_department_not_found";

    // Designation (Task 12.1)
    public const string DesignationCompanyRequired = "designation_company_required";
    public const string DesignationNameRequired = "designation_name_required";
    public const string DesignationNameTooLong = "designation_name_too_long";

    // Employee (Task 12.1)
    public const string EmployeeNumberRequired = "employee_number_required";
    public const string EmployeeNumberTooLong = "employee_number_too_long";
    public const string DuplicateEmployeeNumber = "duplicate_employee_number";
    public const string EmployeeFirstNameRequired = "employee_first_name_required";
    public const string EmployeeFirstNameTooLong = "employee_first_name_too_long";
    public const string EmployeeLastNameRequired = "employee_last_name_required";
    public const string EmployeeLastNameTooLong = "employee_last_name_too_long";
    public const string EmployeeEmailRequired = "employee_email_required";
    public const string InvalidEmployeeEmail = "invalid_employee_email";
    public const string BankNameRequired = "bank_name_required";
    public const string BankAccountNumberRequired = "bank_account_number_required";
    public const string RelievingBeforeJoining = "relieving_before_joining";
    public const string EmployeeNotFound = "employee_not_found";
    public const string InactiveEmployee = "inactive_employee";
    public const string DepartmentNotFound = "department_not_found";
    public const string DesignationNotFound = "designation_not_found";
    public const string CompanyNotFound = "company_not_found";

    // Salary component / structure (Task 12.2)
    public const string ComponentNameRequired = "component_name_required";
    public const string ComponentNameTooLong = "component_name_too_long";
    public const string ComponentNotFound = "component_not_found";
    public const string InactiveComponent = "inactive_component";
    public const string StructureNameRequired = "structure_name_required";
    public const string StructureNameTooLong = "structure_name_too_long";
    public const string StructureNotFound = "structure_not_found";
    public const string InactiveStructure = "inactive_structure";
    public const string NoStructureLines = "no_structure_lines";
    public const string InvalidLineAmount = "invalid_line_amount";
    public const string InvalidLinePercentage = "invalid_line_percentage";
    public const string OverlappingAssignment = "overlapping_assignment";
    public const string InvalidEffectiveDates = "invalid_effective_dates";

    // General Ledger configuration / invariants (Constitution Article III)
    public const string InvalidGlAccount = "invalid_gl_account";
}
