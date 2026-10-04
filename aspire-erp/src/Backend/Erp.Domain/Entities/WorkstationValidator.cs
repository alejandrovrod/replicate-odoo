using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the Workstation master (Task 9.1). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here is unit-tested
/// from <c>tests/Erp.Domain.UnitTests</c> without a database. Mirrors <see cref="ItemValidator"/>
/// static-guard style.
/// </summary>
public static class WorkstationValidator
{
    public const int MaxNameLength = 100;

    /// <summary>Field-level rules: required name (100).</summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidFields(string? workstationName)
    {
        if (string.IsNullOrWhiteSpace(workstationName))
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.WorkstationNameRequired,
                "Workstation Name is required.");
        }

        if (workstationName.Length > MaxNameLength)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.WorkstationNameTooLong,
                $"Workstation Name must not exceed {MaxNameLength} characters.");
        }
    }

    /// <summary>Rate rules: labor, electricity and rent hourly rates must all be &gt;= 0.</summary>
    /// <exception cref="ManufacturingValidationException">A rate is negative.</exception>
    public static void EnsureValidRates(decimal hourRateLabor, decimal hourRateElectricity, decimal hourRateRent)
    {
        if (hourRateLabor < 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.NegativeHourRate,
                $"Hourly labor rate must not be negative (received {hourRateLabor}).");
        }

        if (hourRateElectricity < 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.NegativeHourRate,
                $"Hourly electricity rate must not be negative (received {hourRateElectricity}).");
        }

        if (hourRateRent < 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.NegativeHourRate,
                $"Hourly rent rate must not be negative (received {hourRateRent}).");
        }
    }
}
