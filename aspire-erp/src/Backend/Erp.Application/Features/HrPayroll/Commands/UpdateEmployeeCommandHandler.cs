using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="UpdateEmployeeCommand"/>: loads the employee (company mismatch surfaces as
/// NOT FOUND - no cross-company leak), applies the rowversion check BEFORE any mutation, runs the
/// pure <see cref="EmployeeValidator"/> rules plus a duplicate-number pre-check when the number
/// changed, and saves through the repository (which translates a live EF race into
/// <c>concurrency_conflict</c>). A rejected update writes ZERO rows.
/// </summary>
public sealed class UpdateEmployeeCommandHandler
    : ICommandHandler<UpdateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IHrPayrollRepository _hr;

    public UpdateEmployeeCommandHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<Result<EmployeeDto>> HandleAsync(
        UpdateEmployeeCommand command,
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

            EnsureRowVersion(employee, command.RowVersion);

            if (!string.Equals(employee.EmployeeNumber, employeeNumber, StringComparison.Ordinal))
            {
                var clash = await _hr.GetEmployeeByNumberAsync(employee.CompanyId, employeeNumber, cancellationToken);
                if (clash is not null && clash.Id != employee.Id)
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.DuplicateEmployeeNumber,
                        $"Employee number '{employeeNumber}' already exists in this company.");
                }
            }

            employee.EmployeeNumber = employeeNumber;
            employee.FirstName = firstName;
            employee.LastName = lastName;
            employee.WorkEmail = workEmail;
            employee.DateOfJoining = command.DateOfJoining;
            employee.DateOfRelieving = command.DateOfRelieving;
            employee.SalaryMode = command.SalaryMode;
            employee.BankName = bankName;
            employee.BankAccountNumber = bankAccountNumber;
            employee.Status = command.Status;

            await _hr.UpdateEmployeeAsync(employee, cancellationToken);

            return Result<EmployeeDto>.Success(EmployeeDto.Build(employee));
        }
        catch (HrValidationException ex)
        {
            return Result<EmployeeDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<EmployeeDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static void EnsureRowVersion(Employee employee, byte[]? expected)
    {
        if (expected is null || expected.Length == 0)
        {
            return;
        }

        if (employee.RowVersion is null || !employee.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(Employee), employee.Id);
        }
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
