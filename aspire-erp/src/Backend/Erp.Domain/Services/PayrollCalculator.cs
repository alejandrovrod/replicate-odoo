namespace Erp.Domain.Services;

/// <summary>
/// Net pay calculation engine (spec invariant HR-01, plan.md §2 VERBATIM): net = earnings −
/// deductions, floored at zero. Pure Domain (no EF, no NuGet - Constitution Article I.2),
/// unit-tested without a database.
/// </summary>
/// <remarks>
/// BOUNDARY (documented, not implemented here): when deductions exceed gross earnings the net
/// is clamped to zero and the surplus is ABSORBED (written off), not carried forward. The
/// spec glossary says "surplus deduction is deferred"; deferral (carrying the excess into the
/// next period's slip) is a Block B payroll-engine concern - it needs stored slip state the
/// pure calculator has no access to. The clamp itself is the tested contract.
/// </remarks>
public sealed class PayrollCalculator
{
    public static (decimal grossPay, decimal totalDeductions, decimal netPay) CalculateSalarySlip(
        decimal baseEarnings,
        IEnumerable<decimal> allowances,
        IEnumerable<decimal> deductions)
    {
        var gross = baseEarnings + allowances.Sum();
        var totalDed = deductions.Sum();
        var net = Math.Max(0.0m, gross - totalDed);

        return (gross, totalDed, net);
    }

    /// <summary>
    /// Structure-line roll-up (Task 12.2 documented extension of the plan §2 verbatim core):
    /// each line contributes Amount + PercentageOfBase% x <paramref name="baseEarningsTotal"/>,
    /// split into earnings and deductions totals by the line's component type. Line value math
    /// is the minimal "percentage/formulaic" support - no expression engine.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A line amount or percentage is negative.</exception>
    public static (decimal totalEarnings, decimal totalDeductions) CalculateStructureTotals(
        IEnumerable<StructureLineInput> lines,
        decimal baseEarningsTotal)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (baseEarningsTotal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseEarningsTotal),
                $"Base earnings total must not be negative (received {baseEarningsTotal}).");
        }

        var totalEarnings = 0m;
        var totalDeductions = 0m;

        foreach (var line in lines)
        {
            if (line.Amount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lines),
                    $"Structure line amount must not be negative (received {line.Amount}).");
            }

            if (line.PercentageOfBase.HasValue && line.PercentageOfBase.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lines),
                    $"Structure line percentage must not be negative (received {line.PercentageOfBase.Value}).");
            }

            var value = line.Amount
                + (line.PercentageOfBase.HasValue
                    ? baseEarningsTotal * line.PercentageOfBase.Value / 100m
                    : 0m);

            if (line.IsEarning)
            {
                totalEarnings += value;
            }
            else
            {
                totalDeductions += value;
            }
        }

        return (totalEarnings, totalDeductions);
    }
}

/// <summary>One structure line reduced to its pricing inputs: fixed amount, optional base share, side.</summary>
/// <param name="Amount">Fixed money part; must be &gt;= 0.</param>
/// <param name="PercentageOfBase">Share of the base-earnings total in percent; null means none; must be &gt;= 0 when set.</param>
/// <param name="IsEarning">True for an earning line, false for a deduction line.</param>
public sealed record StructureLineInput(decimal Amount, decimal? PercentageOfBase, bool IsEarning);
