using Erp.Domain.Services;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 10.3 / invariants AS-01: the straight-line schedule generator (pure Domain) against the
/// numbers of spec scenario AS-01 ($2,400 laptop, $0 salvage, 24 months → 24 × $100), the
/// salvage floor, the last-line rounding plug (Σ == gross − salvage EXACTLY) and the input
/// guards (BCL ArgumentOutOfRangeException - domain-typed failures belong to the calling
/// command, not the pure engine).
/// </summary>
public sealed class DepreciationSchedulerTests
{
    private static readonly DateOnly AvailableForUse = new(2026, 10, 1);

    [Fact]
    public void Generate_As01Scenario_Produces24LinesOf100AndSumsExactly2400()
    {
        var schedule = DepreciationScheduler.GenerateStraightLineSchedule(
            2400m, 0m, 24, 1, AvailableForUse);

        Assert.Equal(24, schedule.Count);
        Assert.All(schedule, line => Assert.Equal(100m, line.DepreciationAmount));
        Assert.Equal(2400m, schedule.Sum(line => line.DepreciationAmount));

        // Running totals accumulate line by line to the full depreciable base.
        Assert.Equal(100m, schedule[0].AccumulatedDepreciation);
        Assert.Equal(2400m, schedule[^1].AccumulatedDepreciation);

        // Monthly progression starting one frequency after the available-for-use date.
        Assert.Equal(new DateOnly(2026, 11, 1), schedule[0].ScheduleDate);
        Assert.Equal(new DateOnly(2028, 10, 1), schedule[^1].ScheduleDate);
    }

    [Fact]
    public void Generate_WithSalvageValue_SpreadsNetBaseAndRespectsTheNbvFloor()
    {
        var schedule = DepreciationScheduler.GenerateStraightLineSchedule(
            2400m, 400m, 20, 1, AvailableForUse);

        Assert.Equal(20, schedule.Count);
        Assert.All(schedule, line => Assert.Equal(100m, line.DepreciationAmount));
        Assert.Equal(2000m, schedule.Sum(line => line.DepreciationAmount));

        // NBV floor respected: gross − accumulated == salvage exactly.
        Assert.Equal(400m, 2400m - schedule[^1].AccumulatedDepreciation);
    }

    [Fact]
    public void Generate_WithRepeatingDecimals_LastLinePlugsTheRoundingSoSumIsExact()
    {
        var schedule = DepreciationScheduler.GenerateStraightLineSchedule(
            1000m, 0m, 3, 1, AvailableForUse);

        Assert.Equal(3, schedule.Count);
        Assert.Equal(333.3333m, schedule[0].DepreciationAmount);
        Assert.Equal(333.3333m, schedule[1].DepreciationAmount);
        Assert.Equal(333.3334m, schedule[2].DepreciationAmount);
        Assert.Equal(1000m, schedule.Sum(line => line.DepreciationAmount));
        Assert.Equal(1000m, schedule[^1].AccumulatedDepreciation);
    }

    [Fact]
    public void Generate_WithQuarterlyFrequency_StepsThreeMonthsPerLine()
    {
        var start = new DateOnly(2026, 1, 15);

        var schedule = DepreciationScheduler.GenerateStraightLineSchedule(
            1200m, 0m, 4, 3, start);

        Assert.Equal(4, schedule.Count);
        Assert.Equal(new DateOnly(2026, 4, 15), schedule[0].ScheduleDate);
        Assert.Equal(new DateOnly(2026, 7, 15), schedule[1].ScheduleDate);
        Assert.Equal(new DateOnly(2026, 10, 15), schedule[2].ScheduleDate);
        Assert.Equal(new DateOnly(2027, 1, 15), schedule[3].ScheduleDate);
        Assert.All(schedule, line => Assert.Equal(300m, line.DepreciationAmount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Generate_NonPositiveGross_Throws(decimal gross)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DepreciationScheduler.GenerateStraightLineSchedule(gross, 0m, 12, 1, AvailableForUse));
    }

    [Fact]
    public void Generate_NegativeSalvage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DepreciationScheduler.GenerateStraightLineSchedule(2400m, -1m, 24, 1, AvailableForUse));
    }

    [Fact]
    public void Generate_SalvageAboveGross_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DepreciationScheduler.GenerateStraightLineSchedule(2400m, 2400.0001m, 24, 1, AvailableForUse));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-12)]
    public void Generate_NonPositiveDepreciationCount_Throws(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DepreciationScheduler.GenerateStraightLineSchedule(2400m, 0m, count, 1, AvailableForUse));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Generate_NonPositiveFrequency_Throws(int frequency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DepreciationScheduler.GenerateStraightLineSchedule(2400m, 0m, 24, frequency, AvailableForUse));
    }
}
