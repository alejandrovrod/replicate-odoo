using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 9.1 acceptance ("Calculates composite hourly operating cost automatically"): the composite
/// total is the exact sum of the three rates, negative rates are rejected with a typed code, and
/// the active flag defaults to true.
/// </summary>
public sealed class WorkstationTests
{
    [Fact]
    public void HourRateTotal_IsExactSumOfTheThreeRates()
    {
        var workstation = new Workstation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            WorkstationName = "WS-01",
            HourRateLabor = 25m,
            HourRateElectricity = 10m,
            HourRateRent = 5m,
        };

        Assert.Equal(40m, workstation.HourRateTotal);
    }

    [Fact]
    public void HourRateTotal_MatchesSpecMf01WorkstationRate()
    {
        // Spec MF-01: WS-01 runs at $40.00/hour, so 30 minutes cost exactly $20.00.
        var workstation = new Workstation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            WorkstationName = "WS-01",
            HourRateLabor = 40m,
        };

        Assert.Equal(40m, workstation.HourRateTotal);
        Assert.Equal(20m, BomOperation.CalculateCost(durationMinutes: 30m, hourRateTotal: workstation.HourRateTotal));
    }

    [Fact]
    public void IsActive_DefaultsToTrue()
    {
        Assert.True(new Workstation().IsActive);
    }

    [Fact]
    public void IsActive_CanBeSetToFalse()
    {
        var workstation = new Workstation { IsActive = false };

        Assert.False(workstation.IsActive);
    }

    [Theory]
    [InlineData(-0.0001, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -25)]
    public void EnsureValidRates_NegativeRate_ThrowsWithTypedCode(
        decimal labor, decimal electricity, decimal rent)
    {
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => WorkstationValidator.EnsureValidRates(labor, electricity, rent));

        Assert.Equal(ManufacturingErrorCodes.NegativeHourRate, ex.Code);
        Assert.Equal("negative_hour_rate", ex.Code);
    }

    [Fact]
    public void EnsureValidRates_ZeroRates_AreAllowed()
    {
        WorkstationValidator.EnsureValidRates(0m, 0m, 0m); // must not throw
    }

    [Fact]
    public void EnsureValidFields_BlankName_ThrowsWithTypedCode()
    {
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => WorkstationValidator.EnsureValidFields("  "));

        Assert.Equal(ManufacturingErrorCodes.WorkstationNameRequired, ex.Code);
    }

    [Fact]
    public void EnsureValidFields_NameOverLimit_ThrowsWithTypedCode()
    {
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => WorkstationValidator.EnsureValidFields(new string('W', WorkstationValidator.MaxNameLength + 1)));

        Assert.Equal(ManufacturingErrorCodes.WorkstationNameTooLong, ex.Code);
    }
}
