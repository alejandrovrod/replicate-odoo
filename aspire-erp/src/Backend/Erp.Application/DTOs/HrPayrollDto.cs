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
