using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="CreateEmployeeCommand"/>: pure field rules through the
/// <see cref="EmployeeValidator"/>, then the company + duplicate-number + (optional) structure
/// checks before the employee and its initial assignment persist in ONE transaction. A rejected
/// creation writes ZERO rows.
/// </summary>
public sealed class CreateEmployeeCommandHandler
    : ICommandHandler<CreateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IHrPayrollRepository _hr;

    public CreateEmployeeCommandHandler(ICompanyRepository companies, IHrPayrollRepository hr)
    {
        _companies = companies;
        _hr = hr;
    }

    public async Task<Result<EmployeeDto>> HandleAsync(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var employeeNumber = Normalize(command.EmployeeNumber);
            var firstName = Normalize(command.FirstName);
            var lastName = Normalize(command.LastName);
            var workEmail = Normalize(command.WorkEmail);
            var bankName = NormalizeOptional(command.BankName);
            var bankAccountNumber = NormalizeOptional(command.BankAccountNumber);

            EmployeeValidator.EnsureValidEmployeeNumber(employeeNumber);
            EmployeeValidator.EnsureValidName(firstName, lastName);
            EmployeeValidator.EnsureValidEmail(workEmail);
            EmployeeValidator.EnsureValidEmploymentDates(command.DateOfJoining, command.DateOfRelieving);
            EmployeeValidator.EnsureValidBankDetails(command.Status, command.SalaryMode, bankName, bankAccountNumber);

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new HrValidationException(
                    HrPayrollErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            // The DB unique index (UQ_Employee_Tenant_Company_Code) is the authority; this
            // pre-check is the 409 UX (same note as Account/ Customer).
            var existing = await _hr.GetEmployeeByNumberAsync(company.Id, employeeNumber, cancellationToken);
            if (existing is not null)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.DuplicateEmployeeNumber,
                    $"Employee number '{employeeNumber}' already exists in this company.");
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                EmployeeNumber = employeeNumber,
                FirstName = firstName,
                LastName = lastName,
                WorkEmail = workEmail,
                DateOfJoining = command.DateOfJoining,
                DateOfRelieving = command.DateOfRelieving,
                SalaryMode = command.SalaryMode,
                BankName = bankName,
                BankAccountNumber = bankAccountNumber,
                Status = command.Status,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,

                // TenantId is intentionally NOT set: AppDbContext stamps it on insert (II.4).
            };

            var assignment = await ResolveInitialAssignmentAsync(command, company.Id, employee.Id, cancellationToken);

            await _hr.ExecuteInTransactionAsync(
                async token =>
                {
                    await _hr.AddEmployeeAsync(employee, token);
                    if (assignment is not null)
                    {
                        await _hr.AddAssignmentAsync(assignment, token);
                    }

                    return true;
                },
                cancellationToken);

            return Result<EmployeeDto>.Success(EmployeeDto.Build(employee));
        }
        catch (HrValidationException ex)
        {
            return Result<EmployeeDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Resolves the optional initial structure assignment (created atomically with the employee).
    /// A missing/foreign/inactive structure rejects the whole creation. The window is
    /// [DateOfJoining, null) - a brand-new employee has no prior assignments, so it never overlaps.
    /// </summary>
    private async Task<SalaryStructureAssignment?> ResolveInitialAssignmentAsync(
        CreateEmployeeCommand command,
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        if (command.SalaryStructureId is not { } structureId || structureId == Guid.Empty)
        {
            return null;
        }

        var structure = await _hr.GetStructureByIdAsync(structureId, cancellationToken)
            ?? throw new HrValidationException(
                HrPayrollErrorCodes.StructureNotFound,
                $"Salary structure '{structureId}' was not found in this tenant.");

        if (structure.CompanyId != companyId)
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

        return new SalaryStructureAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeId = employeeId,
            StructureId = structure.Id,
            EffectiveFrom = command.DateOfJoining,
            EffectiveTo = null,
            IsActive = true,

            // TenantId is intentionally NOT set: AppDbContext stamps it on insert (II.4).
        };
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
