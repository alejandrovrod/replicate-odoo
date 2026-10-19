using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// Employee directory row (Task 12.5 read): identity, org links, dates, lifecycle and the bank
/// data the edit form round-trips, plus the <see cref="RowVersion"/> optimistic token (base64)
/// echoed back on PUT. Guarded by <c>permission:employee:read</c>.
/// </summary>
public sealed record EmployeeDto(
    Guid Id,
    Guid CompanyId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string WorkEmail,
    Guid? DepartmentId,
    Guid? DesignationId,
    DateOnly DateOfJoining,
    DateOnly? DateOfRelieving,
    SalaryMode SalaryMode,
    string? BankName,
    string? BankAccountNumber,
    EmploymentStatus Status,
    bool IsActive,
    string RowVersion)
{
    public static EmployeeDto Build(Employee employee) =>
        new(
            employee.Id,
            employee.CompanyId,
            employee.EmployeeNumber,
            employee.FirstName,
            employee.LastName,
            employee.WorkEmail,
            employee.DepartmentId,
            employee.DesignationId,
            employee.DateOfJoining,
            employee.DateOfRelieving,
            employee.SalaryMode,
            employee.BankName,
            employee.BankAccountNumber,
            employee.Status,
            employee.IsActive,
            employee.RowVersion is null || employee.RowVersion.Length == 0
                ? string.Empty
                : Convert.ToBase64String(employee.RowVersion));
}

/// <summary>Salary component payload (Task 12.2): the pay element plus its GL posting account.</summary>
public sealed record SalaryComponentDto(
    Guid Id,
    Guid CompanyId,
    string ComponentName,
    SalaryComponentType ComponentType,
    bool DependsOnPaymentDays,
    bool IsTaxApplicable,
    Guid DefaultGLAccountId,
    bool IsActive)
{
    public static SalaryComponentDto Build(SalaryComponent component) =>
        new(
            component.Id,
            component.CompanyId,
            component.ComponentName,
            component.ComponentType,
            component.DependsOnPaymentDays,
            component.IsTaxApplicable,
            component.DefaultGLAccountId,
            component.IsActive);
}

/// <summary>One structure line payload (Task 12.2): fixed amount plus optional base percentage.</summary>
public sealed record SalaryStructureLineDto(
    Guid Id,
    Guid ComponentId,
    string ComponentName,
    SalaryComponentType ComponentType,
    decimal Amount,
    decimal? PercentageOfBase);

/// <summary>Salary structure payload (Task 12.2): header plus its priced component lines.</summary>
public sealed record SalaryStructureDto(
    Guid Id,
    Guid CompanyId,
    string StructureName,
    bool IsActive,
    IReadOnlyList<SalaryStructureLineDto> Lines)
{
    public static SalaryStructureDto Build(SalaryStructure structure, IReadOnlyDictionary<Guid, SalaryComponent> componentsById) =>
        new(
            structure.Id,
            structure.CompanyId,
            structure.StructureName,
            structure.IsActive,
            structure.Lines
                .Select(l =>
                {
                    componentsById.TryGetValue(l.ComponentId, out var component);
                    return new SalaryStructureLineDto(
                        l.Id,
                        l.ComponentId,
                        component?.ComponentName ?? string.Empty,
                        component?.ComponentType ?? SalaryComponentType.Earning,
                        l.Amount,
                        l.PercentageOfBase);
                })
                .ToList());
}

/// <summary>Structure assignment payload (Task 12.2): the employee-to-structure binding window.</summary>
public sealed record SalaryStructureAssignmentDto(
    Guid Id,
    Guid EmployeeId,
    Guid StructureId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive)
{
    public static SalaryStructureAssignmentDto Build(SalaryStructureAssignment assignment) =>
        new(
            assignment.Id,
            assignment.EmployeeId,
            assignment.StructureId,
            assignment.EffectiveFrom,
            assignment.EffectiveTo,
            assignment.IsActive);
}

/// <summary>One priced slip-line payload (Task 12.3): the itemized HR-01 stub row.</summary>
public sealed record SalarySlipLineDto(
    Guid Id,
    Guid ComponentId,
    string ComponentName,
    SalaryComponentType ComponentType,
    decimal Amount);

/// <summary>One employee pay stub payload (Task 12.3): stored HR-01 amounts plus itemized lines.</summary>
public sealed record SalarySlipDto(
    Guid Id,
    Guid EmployeeId,
    string SlipNumber,
    int PaymentDays,
    int AbsentDays,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    SalarySlipStatus Status,
    IReadOnlyList<SalarySlipLineDto> Lines)
{
    public static SalarySlipDto Build(SalarySlip slip) =>
        new(
            slip.Id,
            slip.EmployeeId,
            slip.SlipNumber,
            slip.PaymentDays,
            slip.AbsentDays,
            slip.GrossPay,
            slip.TotalDeductions,
            slip.NetPay,
            slip.Status,
            slip.Lines
                .OrderBy(l => l.ComponentName)
                .Select(l => new SalarySlipLineDto(
                    l.Id, l.ComponentId, l.ComponentName, l.ComponentType, l.Amount))
                .ToList());
}

/// <summary>Payroll batch header payload (Tasks 12.3-12.4): stored totals plus voucher links.</summary>
public sealed record PayrollEntryDto(
    Guid Id,
    Guid CompanyId,
    string PayrollNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PostingDate,
    PayrollEntryStatus Status,
    decimal TotalGrossPay,
    decimal TotalDeductions,
    decimal TotalNetPay,
    string? AccrualVoucherNo,
    string? PaymentVoucherNo,
    string RowVersion,
    int SlipCount)
{
    public static PayrollEntryDto Build(PayrollEntry entry, int slipCount) =>
        new(
            entry.Id,
            entry.CompanyId,
            entry.PayrollNumber,
            entry.StartDate,
            entry.EndDate,
            entry.PostingDate,
            entry.Status,
            entry.TotalGrossPay,
            entry.TotalDeductions,
            entry.TotalNetPay,
            entry.AccrualVoucherNo,
            entry.PaymentVoucherNo,
            entry.RowVersion is null || entry.RowVersion.Length == 0
                ? string.Empty
                : Convert.ToBase64String(entry.RowVersion),
            slipCount);
}

/// <summary>One skipped employee of a submit run (Task 12.3 replay-safe report, AS-04 precedent).</summary>
public sealed record PayrollSkipDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string Reason);

/// <summary>Submit-run outcome (Task 12.3): the submitted entry plus the skip-and-report list.</summary>
public sealed record PayrollSubmitResultDto(
    PayrollEntryDto Entry,
    int CreatedSlipCount,
    IReadOnlyList<PayrollSkipDto> Skipped);

/// <summary>Batch detail payload (Task 12.4 reads): the header plus every slip with its lines.</summary>
public sealed record PayrollEntryDetailDto(
    PayrollEntryDto Entry,
    IReadOnlyList<SalarySlipDto> Slips);

/// <summary>Company fiscal header of a printable pay stub (name, tax id, country).</summary>
public sealed record SalarySlipCompanyDto(
    string Name,
    string TaxId,
    string? CountryCode);

/// <summary>Employee identity block of a printable pay stub.</summary>
public sealed record SalarySlipEmployeeDto(
    string EmployeeNumber,
    string FullName,
    string WorkEmail);

/// <summary>One priced line of a printable pay stub (component snapshot + amount).</summary>
public sealed record SalarySlipPrintableLineDto(
    string ComponentName,
    SalaryComponentType ComponentType,
    decimal Amount);

/// <summary>
/// Printable pay stub (spec: formal receipt view): split earnings/deductions, the settlement
/// (posting) date and the company fiscal header. Employer contributions are intentionally NOT
/// part of the domain model (SalaryComponentType is Earning|Deduction only), so they are absent
/// by design rather than rendered empty.
/// </summary>
public sealed record SalarySlipPrintableDto(
    Guid SlipId,
    string SlipNumber,
    Guid PayrollEntryId,
    string PayrollNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly PostingDate,
    SalarySlipStatus Status,
    int PaymentDays,
    int AbsentDays,
    SalarySlipCompanyDto Company,
    SalarySlipEmployeeDto Employee,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    IReadOnlyList<SalarySlipPrintableLineDto> Earnings,
    IReadOnlyList<SalarySlipPrintableLineDto> Deductions)
{
    public static SalarySlipPrintableDto Build(
        PayrollEntry entry,
        SalarySlip slip,
        Company company,
        Employee? employee)
    {
        static SalarySlipPrintableLineDto Line(SalarySlipLine line) =>
            new(line.ComponentName, line.ComponentType, line.Amount);

        var earnings = slip.Lines
            .Where(l => l.ComponentType == SalaryComponentType.Earning)
            .OrderBy(l => l.ComponentName)
            .Select(Line)
            .ToList();

        var deductions = slip.Lines
            .Where(l => l.ComponentType == SalaryComponentType.Deduction)
            .OrderBy(l => l.ComponentName)
            .Select(Line)
            .ToList();

        return new SalarySlipPrintableDto(
            slip.Id,
            slip.SlipNumber,
            entry.Id,
            entry.PayrollNumber,
            entry.StartDate,
            entry.EndDate,
            entry.PostingDate,
            slip.Status,
            slip.PaymentDays,
            slip.AbsentDays,
            new SalarySlipCompanyDto(company.Name, company.TaxId, company.CountryCode),
            new SalarySlipEmployeeDto(
                employee?.EmployeeNumber ?? string.Empty,
                employee is null ? string.Empty : $"{employee.FirstName} {employee.LastName}".Trim(),
                employee?.WorkEmail ?? string.Empty),
            slip.GrossPay,
            slip.TotalDeductions,
            slip.NetPay,
            earnings,
            deductions);
    }
}
