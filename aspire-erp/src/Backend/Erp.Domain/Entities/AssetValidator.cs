using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field rules for the asset aggregates (Tasks 10.1-10.2). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free. Mirrors the
/// <see cref="WorkOrderValidator"/> static-guard style. Status transitions themselves live on
/// <see cref="Asset"/>; this validator covers creation/capitalization-time field guards only.
/// </summary>
public static class AssetValidator
{
    private const int MaxCategoryNameLength = 100;

    /// <summary>Category display name is required and fits the plan DDL NVARCHAR(100).</summary>
    /// <exception cref="AssetValidationException">An invariant was violated.</exception>
    public static void EnsureValidCategoryName(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new AssetValidationException(
                AssetErrorCodes.CategoryNameRequired,
                "An asset category requires a name.");
        }

        if (categoryName.Length > MaxCategoryNameLength)
        {
            throw new AssetValidationException(
                AssetErrorCodes.CategoryNameTooLong,
                $"An asset category name must not exceed {MaxCategoryNameLength} characters (received {categoryName.Length}).");
        }
    }

    /// <summary>Gross purchase cost is strictly positive (CK_Asset_Values).</summary>
    /// <exception cref="AssetValidationException">An invariant was violated.</exception>
    public static void EnsureValidGrossAmount(decimal grossPurchaseAmount)
    {
        if (grossPurchaseAmount <= 0)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidGrossAmount,
                $"Gross purchase amount must be greater than zero (received {grossPurchaseAmount:0.####}).");
        }
    }

    /// <summary>
    /// Salvage value is non-negative and never exceeds the gross cost (AS-01: the depreciable
    /// base gross − salvage must be &gt;= 0, and NBV &gt;= SalvageValue must stay satisfiable).
    /// </summary>
    /// <exception cref="AssetValidationException">An invariant was violated.</exception>
    public static void EnsureValidSalvageValue(decimal grossPurchaseAmount, decimal salvageValue)
    {
        if (salvageValue < 0)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidSalvageValue,
                $"Salvage value must not be negative (received {salvageValue:0.####}).");
        }

        if (salvageValue > grossPurchaseAmount)
        {
            throw new AssetValidationException(
                AssetErrorCodes.SalvageExceedsCost,
                $"Salvage value ({salvageValue:0.####}) must not exceed gross purchase amount ({grossPurchaseAmount:0.####}).");
        }
    }

    /// <summary>Schedule sizing is strictly positive (CK_Asset_Periods).</summary>
    /// <exception cref="AssetValidationException">An invariant was violated.</exception>
    public static void EnsureValidDepreciationPeriods(int totalNumberOfDepreciations, int frequencyInMonths)
    {
        if (totalNumberOfDepreciations <= 0)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidDepreciationPeriods,
                $"Total number of depreciations must be greater than zero (received {totalNumberOfDepreciations}).");
        }

        if (frequencyInMonths <= 0)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidDepreciationPeriods,
                $"Frequency in months must be greater than zero (received {frequencyInMonths}).");
        }
    }
}
