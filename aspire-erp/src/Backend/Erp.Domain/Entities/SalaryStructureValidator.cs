using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# shape rules for the salary structure aggregate (Task 12.2). Mirrors the
/// <see cref="AssetValidator"/> static-guard style. Deliberate split: the EMPLOYEE checks
/// (exists + active) need repository reads, so they live in the assign COMMAND handler (which
/// has repo access) - this validator keeps only pure shape checks plus the overlap detection
/// as a pure function over date ranges.
/// </summary>
public static class SalaryStructureValidator
{
    public const int MaxNameLength = 100;

    /// <summary>Component display name is required and fits the plan DDL NVARCHAR(100).</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidComponentName(string? componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.ComponentNameRequired,
                "A salary component requires a name.");
        }

        if (componentName.Length > MaxNameLength)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.ComponentNameTooLong,
                $"A salary component name must not exceed {MaxNameLength} characters.");
        }
    }

    /// <summary>Structure display name is required and fits the NVARCHAR(100) convention.</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidStructureName(string? structureName)
    {
        if (string.IsNullOrWhiteSpace(structureName))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.StructureNameRequired,
                "A salary structure requires a name.");
        }

        if (structureName.Length > MaxNameLength)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.StructureNameTooLong,
                $"A salary structure name must not exceed {MaxNameLength} characters.");
        }
    }

    /// <summary>
    /// A structure carries at least one line; every line amount is &gt;= 0 and every set
    /// percentage is &gt;= 0.
    /// </summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidLines(IReadOnlyList<(decimal Amount, decimal? PercentageOfBase)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.NoStructureLines,
                "A salary structure requires at least one component line.");
        }

        foreach (var (amount, percentageOfBase) in lines)
        {
            if (amount < 0)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.InvalidLineAmount,
                    $"A structure line amount must not be negative (received {amount:0.####}).");
            }

            if (percentageOfBase.HasValue && percentageOfBase.Value < 0)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.InvalidLinePercentage,
                    $"A structure line percentage must not be negative (received {percentageOfBase.Value:0.####}).");
            }
        }
    }

    /// <summary>Assignment effective dates: EffectiveTo, when set, must not precede EffectiveFrom.</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidEffectiveDates(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidEffectiveDates,
                $"Effective To ({effectiveTo.Value:yyyy-MM-dd}) must not precede Effective From ({effectiveFrom:yyyy-MM-dd}).");
        }
    }

    /// <summary>
    /// Overlap detection as a pure function over date ranges: the new [newFrom, newTo] window
    /// must not intersect any existing assignment window of the same employee. Null EffectiveTo
    /// means open-ended (treated as infinite). Both boundaries are inclusive.
    /// </summary>
    /// <exception cref="HrValidationException">An overlap was detected.</exception>
    public static void EnsureNoOverlap(
        IReadOnlyList<(DateOnly From, DateOnly? To)> existingAssignments,
        DateOnly newFrom,
        DateOnly? newTo)
    {
        ArgumentNullException.ThrowIfNull(existingAssignments);

        var newEnd = newTo ?? DateOnly.MaxValue;

        foreach (var (from, to) in existingAssignments)
        {
            var existingEnd = to ?? DateOnly.MaxValue;

            if (newFrom <= existingEnd && from <= newEnd)
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.OverlappingAssignment,
                    $"The new assignment [{newFrom:yyyy-MM-dd}..{(newTo.HasValue ? newTo.Value.ToString("yyyy-MM-dd") : "open")}] "
                    + $"overlaps the existing assignment [{from:yyyy-MM-dd}..{(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "open")}] "
                    + "of the same employee.");
            }
        }
    }
}
