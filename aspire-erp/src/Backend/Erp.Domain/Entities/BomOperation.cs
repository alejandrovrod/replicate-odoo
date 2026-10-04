namespace Erp.Domain.Entities;

/// <summary>
/// One workstation operation of a <see cref="BillOfMaterials"/> (Task 9.2): a timed step on a
/// <see cref="Workstation"/>. The plan DDL has no table for operations (deliberate deviation -
/// without it MF-01's 30-minute WS-01 step cannot be costed), so this entity exists as
/// <c>BomOperation</c> with cascade delete from its BOM.
/// </summary>
/// <remarks>
/// Dependency-free by design (same rule as the cost engine): <see cref="CalculateCost"/> takes
/// the workstation's total hourly rate as a VALUE, never the <see cref="Workstation"/> entity,
/// so operation costing stays unit-testable without a repository.
/// </remarks>
public class BomOperation
{
    public Guid Id { get; set; }

    public Guid BomId { get; set; }

    public BillOfMaterials? Bom { get; set; }

    public Guid WorkstationId { get; set; }

    public Workstation? Workstation { get; set; }

    public string? Description { get; set; }

    /// <summary>Step duration in minutes; strictly positive.</summary>
    public decimal DurationMinutes { get; set; }

    /// <summary>
    /// Operation cost = (durationMinutes / 60) x hourRateTotal, rounded to 4 decimals (the GL
    /// amount scale). Takes the rate as a value so callers pass
    /// <c>workstation.HourRateTotal</c> explicitly.
    /// </summary>
    public static decimal CalculateCost(decimal durationMinutes, decimal hourRateTotal)
    {
        if (durationMinutes <= 0)
        {
            throw new Exceptions.ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidOperationDuration,
                $"Operation duration must be greater than zero minutes (received {durationMinutes}).");
        }

        if (hourRateTotal < 0)
        {
            throw new Exceptions.ManufacturingValidationException(
                ManufacturingErrorCodes.NegativeHourRate,
                $"Workstation hourly rate must not be negative (received {hourRateTotal}).");
        }

        return Math.Round(durationMinutes / 60m * hourRateTotal, 4, MidpointRounding.AwayFromZero);
    }
}
