using Erp.Domain.Services;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 12.2 engine proofs: spec invariant HR-01's own numbers must come out EXACT
/// ($4,000 base + $1,000 allowance − $600 − $400 → gross $5,000, deductions $1,000, net
/// $4,000), the clamp absorbs the surplus when deductions exceed gross, percentage lines
/// price off the base-earnings total, and empty inputs net zero.
/// </summary>
public sealed class PayrollCalculatorTests
{
    [Fact]
    public void CalculateSalarySlip_Hr01Literal_ReturnsFourThousandNet()
    {
        // Spec scenario HR-01 verbatim: Basic $4,000 + Housing $1,000 − Tax $600 − Pension $400.
        var (gross, totalDed, net) = PayrollCalculator.CalculateSalarySlip(
            baseEarnings: 4000m,
            allowances: [1000m],
            deductions: [600m, 400m]);

        Assert.Equal(5000m, gross);
        Assert.Equal(1000m, totalDed);
        Assert.Equal(4000m, net);
    }

    [Fact]
    public void CalculateSalarySlip_DeductionsExceedGross_ClampsToZero()
    {
        // HR-01 floor: net cannot be negative. The surplus is ABSORBED (written off) - deferral
        // into the next period is a Block B engine concern, documented on PayrollCalculator.
        var (gross, totalDed, net) = PayrollCalculator.CalculateSalarySlip(
            baseEarnings: 1000m,
            allowances: [],
            deductions: [800m, 500m]);

        Assert.Equal(1000m, gross);
        Assert.Equal(1300m, totalDed);
        Assert.Equal(0m, net);
    }

    [Fact]
    public void CalculateSalarySlip_EmptyInputs_NetsZero()
    {
        var (gross, totalDed, net) = PayrollCalculator.CalculateSalarySlip(
            baseEarnings: 0m,
            allowances: [],
            deductions: []);

        Assert.Equal(0m, gross);
        Assert.Equal(0m, totalDed);
        Assert.Equal(0m, net);
    }

    [Fact]
    public void CalculateStructureTotals_FixedPlusPercentage_SplitsBySide()
    {
        // Base-earnings total $4,000: Basic fixed $4,000 + Housing fixed $1,000 (earnings),
        // pension 10% of base = $400 (deduction), tax fixed $600 (deduction).
        var (earnings, deductions) = PayrollCalculator.CalculateStructureTotals(
            [
                new StructureLineInput(Amount: 4000m, PercentageOfBase: null, IsEarning: true),
                new StructureLineInput(Amount: 1000m, PercentageOfBase: null, IsEarning: true),
                new StructureLineInput(Amount: 0m, PercentageOfBase: 10m, IsEarning: false),
                new StructureLineInput(Amount: 600m, PercentageOfBase: null, IsEarning: false),
            ],
            baseEarningsTotal: 4000m);

        Assert.Equal(5000m, earnings);
        Assert.Equal(1000m, deductions);
    }

    [Fact]
    public void CalculateStructureTotals_EmptyLines_ReturnsZeros()
    {
        var (earnings, deductions) = PayrollCalculator.CalculateStructureTotals(
            [],
            baseEarningsTotal: 4000m);

        Assert.Equal(0m, earnings);
        Assert.Equal(0m, deductions);
    }

    [Fact]
    public void CalculateStructureTotals_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollCalculator.CalculateStructureTotals(
                [new StructureLineInput(Amount: -1m, PercentageOfBase: null, IsEarning: true)],
                baseEarningsTotal: 4000m));
    }

    [Fact]
    public void CalculateStructureTotals_NegativePercentage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollCalculator.CalculateStructureTotals(
                [new StructureLineInput(Amount: 0m, PercentageOfBase: -5m, IsEarning: false)],
                baseEarningsTotal: 4000m));
    }
}
