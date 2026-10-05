using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

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
