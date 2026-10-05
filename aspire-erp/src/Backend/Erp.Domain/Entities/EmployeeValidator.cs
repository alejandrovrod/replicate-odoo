using System.Text.RegularExpressions;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field rules for the <see cref="Employee"/> aggregate (Task 12.1 acceptance:
/// "active employees have valid email and bank accounts"). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free. Mirrors the
/// <see cref="AssetValidator"/> static-guard style. Eligibility itself lives on
/// <see cref="Employee.IsEligibleForPeriod"/> (spec HR-03).
/// </summary>
public static partial class EmployeeValidator
{
    public const int MaxEmployeeNumberLength = 50;
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 150;

    // RFC-lite: one @, non-empty local and domain parts, at least one dot in the domain.
    // Rejects the common garbage ("plainaddress", "a@b", "@x.com", "a@.com") without pulling
    // in a full RFC 5322 parser - full mailbox validation is a follow-up, not this task.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailPattern();

    /// <summary>Employee number is required and fits the plan DDL NVARCHAR(50).</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidEmployeeNumber(string? employeeNumber)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.EmployeeNumberRequired,
                "Employee Number is required.");
        }

        if (employeeNumber.Length > MaxEmployeeNumberLength)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.EmployeeNumberTooLong,
                $"Employee Number must not exceed {MaxEmployeeNumberLength} characters.");
        }
    }

    /// <summary>First and last names are required and fit the plan DDL NVARCHAR(100).</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.EmployeeFirstNameRequired,
                "Employee First Name is required.");
        }

        if (firstName.Length > MaxNameLength)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.EmployeeFirstNameTooLong,
                $"Employee First Name must not exceed {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.EmployeeLastNameRequired,
                "Employee Last Name is required.");
        }

        if (lastName.Length > MaxNameLength)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.EmployeeLastNameTooLong,
                $"Employee Last Name must not exceed {MaxNameLength} characters.");
        }
    }

    /// <summary>Work email must match the RFC-lite pattern (Task 12.1 acceptance).</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidEmail(string? workEmail)
    {
        if (string.IsNullOrWhiteSpace(workEmail))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.EmployeeEmailRequired,
                "Employee Work Email is required.");
        }

        if (workEmail.Length > MaxEmailLength || !EmailPattern().IsMatch(workEmail))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidEmployeeEmail,
                $"Work Email '{workEmail}' is not a valid email address.");
        }
    }

    /// <summary>
    /// Task 12.1 acceptance ("active employees have valid ... bank accounts"): when the employee
    /// is Active AND paid by Bank, BankName and BankAccountNumber are both required. Any other
    /// (Status, SalaryMode) combination skips the gate - cash/cheque staff carry no bank data,
    /// and departed staff are not paid.
    /// </summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidBankDetails(EmploymentStatus status, SalaryMode salaryMode, string? bankName, string? bankAccountNumber)
    {
        if (status != EmploymentStatus.Active || salaryMode != SalaryMode.Bank)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(bankName))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.BankNameRequired,
                "Bank Name is required for an active employee paid by bank transfer.");
        }

        if (string.IsNullOrWhiteSpace(bankAccountNumber))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.BankAccountNumberRequired,
                "Bank Account Number is required for an active employee paid by bank transfer.");
        }
    }

    /// <summary>Relieving date, when set, must not precede the joining date.</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidEmploymentDates(DateOnly dateOfJoining, DateOnly? dateOfRelieving)
    {
        if (dateOfRelieving.HasValue && dateOfRelieving.Value < dateOfJoining)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.RelievingBeforeJoining,
                $"Date of Relieving ({dateOfRelieving.Value:yyyy-MM-dd}) must not precede Date of Joining ({dateOfJoining:yyyy-MM-dd}).");
        }
    }
}
