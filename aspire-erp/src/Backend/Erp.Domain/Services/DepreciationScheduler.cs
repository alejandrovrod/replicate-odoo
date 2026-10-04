namespace Erp.Domain.Services;

/// <summary>
/// One planned straight-line posting: due date, amount and the running accumulated total.
/// The capitalize handler maps these onto <c>AssetDepreciationSchedule</c> rows.
/// </summary>
public sealed record AssetDepreciationScheduleLine(
    DateOnly ScheduleDate,
    decimal DepreciationAmount,
    decimal AccumulatedDepreciation);

public sealed class DepreciationScheduler
{
    public static List<AssetDepreciationScheduleLine> GenerateStraightLineSchedule(
        decimal grossAmount,
        decimal salvageValue,
        int totalDepreciations,
        int frequencyInMonths,
        DateOnly availableForUseDate)
    {
        if (grossAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grossAmount),
                $"Gross amount must be greater than zero (received {grossAmount}).");
        }

        if (salvageValue < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(salvageValue),
                $"Salvage value must not be negative (received {salvageValue}).");
        }

        if (salvageValue > grossAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(salvageValue),
                $"Salvage value ({salvageValue}) must not exceed gross amount ({grossAmount}).");
        }

        if (totalDepreciations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalDepreciations),
                $"Total depreciations must be greater than zero (received {totalDepreciations}).");
        }

        if (frequencyInMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frequencyInMonths),
                $"Frequency in months must be greater than zero (received {frequencyInMonths}).");
        }

        var depreciableAmount = grossAmount - salvageValue;
        var periodicAmount = Math.Round(depreciableAmount / totalDepreciations, 4);
        var schedule = new List<AssetDepreciationScheduleLine>();
        decimal accumulated = 0.0m;

        for (int i = 1; i <= totalDepreciations; i++)
        {
            var scheduleDate = availableForUseDate.AddMonths(i * frequencyInMonths);
            var isLast = i == totalDepreciations;
            var amount = isLast ? (depreciableAmount - accumulated) : periodicAmount;

            accumulated += amount;
            schedule.Add(new AssetDepreciationScheduleLine(scheduleDate, amount, accumulated));
        }

        return schedule;
    }
}
