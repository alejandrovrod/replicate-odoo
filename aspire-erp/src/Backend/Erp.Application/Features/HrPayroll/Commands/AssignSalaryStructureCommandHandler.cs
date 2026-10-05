using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="AssignSalaryStructureCommand"/> (Task 12.2): the EMPLOYEE checks live
/// here (exists + active needs repo reads - the validator keeps only pure shape checks), the
/// overlap detection runs through the pure
/// <see cref="SalaryStructureValidator.EnsureNoOverlap"/> over the employee's existing
/// windows, and the assignment persists. Tenant-mismatch on either side surfaces as NOT
/// FOUND (no cross-tenant leak).
/// </summary>
public sealed class AssignSalaryStructureCommandHandler
    : ICommandHandler<AssignSalaryStructureCommand, Result<SalaryStructureAssignmentDto>>
{
    private readonly IHrPayrollRepository _hr;

    public AssignSalaryStructureCommandHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<Result<SalaryStructureAssignmentDto>> HandleAsync(
        AssignSalaryStructureCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SalaryStructureValidator.EnsureValidEffectiveDates(command.EffectiveFrom, command.EffectiveTo);

            var employee = await _hr.GetEmployeeByIdAsync(command.EmployeeId, cancellationToken)
                ?? throw new HrValidationException(
                    HrPayrollErrorCodes.EmployeeNotFound,
                    $"Employee '{command.EmployeeId}' was not found in this tenant.");

            if (employee.CompanyId != command.CompanyId)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.EmployeeNotFound,
                    $"Employee '{command.EmployeeId}' was not found in this company.");
            }

            if (employee.Status != EmploymentStatus.Active || !employee.IsActive)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.InactiveEmployee,
                    $"Employee '{employee.EmployeeNumber}' is not active and cannot be assigned a salary structure.");
            }

            var structure = await _hr.GetStructureByIdAsync(command.StructureId, cancellationToken)
                ?? throw new HrValidationException(
                    HrPayrollErrorCodes.StructureNotFound,
                    $"Salary structure '{command.StructureId}' was not found in this tenant.");

            if (structure.CompanyId != command.CompanyId || structure.CompanyId != employee.CompanyId)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.StructureNotFound,
                    $"Salary structure '{structure.StructureName}' belongs to another company.");
            }

            if (!structure.IsActive)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.InactiveStructure,
                    $"Salary structure '{structure.StructureName}' is inactive and cannot be assigned.");
            }

            var existing = await _hr.GetAssignmentsByEmployeeAsync(employee.Id, cancellationToken);

            SalaryStructureValidator.EnsureNoOverlap(
                existing.Select(a => (a.EffectiveFrom, a.EffectiveTo)).ToList(),
                command.EffectiveFrom,
                command.EffectiveTo);

            var assignment = new SalaryStructureAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = employee.CompanyId,
                EmployeeId = employee.Id,
                StructureId = structure.Id,
                EffectiveFrom = command.EffectiveFrom,
                EffectiveTo = command.EffectiveTo,
                IsActive = true,

                // TenantId is intentionally NOT set: AppDbContext stamps it on insert (II.4).
            };

            await _hr.AddAssignmentAsync(assignment, cancellationToken);

            return Result<SalaryStructureAssignmentDto>.Success(
                SalaryStructureAssignmentDto.Build(assignment));
        }
        catch (HrValidationException ex)
        {
            return Result<SalaryStructureAssignmentDto>.Failure(ex.Code, ex.Message);
        }
    }
}
