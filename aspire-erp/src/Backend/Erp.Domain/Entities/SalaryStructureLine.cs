namespace Erp.Domain.Entities;

/// <summary>
/// One component row of a <see cref="SalaryStructure"/> (Task 12.2): which pay element, at
/// what fixed <see cref="Amount"/> plus what share of base earnings
/// (<see cref="PercentageOfBase"/>). Line value = Amount + PercentageOfBase% x base-earnings
/// total (see <c>PayrollCalculator.CalculateStructureTotals</c>). This is the MINIMAL
/// "percentage/formulaic" support from the spec glossary: fixed amounts plus a single
/// percentage-of-base - there is NO expression engine (no formulas referencing other
/// components, no conditionals). Anything richer is a follow-up, not a silent extension.
/// </summary>
public class SalaryStructureLine
{
    public Guid Id { get; set; }

    public Guid StructureId { get; set; }

    public SalaryStructure? Structure { get; set; }

    public Guid ComponentId { get; set; }

    public SalaryComponent? Component { get; set; }

    /// <summary>Fixed money part of the line, decimal(18,4), must be &gt;= 0.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Percentage of the base-earnings total, decimal(5,2), nullable, must be &gt;= 0 when set.
    /// Null means "no percentage part" (pure fixed line).
    /// </summary>
    public decimal? PercentageOfBase { get; set; }
}
