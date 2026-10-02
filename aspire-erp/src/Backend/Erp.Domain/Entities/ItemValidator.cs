using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the Item aggregate (Task 3.1). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here is unit-tested
/// from <c>tests/Erp.Domain.UnitTests</c> without a database.
/// </summary>
public static class ItemValidator
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;
    public const int MaxUomCodeLength = 20;
    public const int MaxUomNameLength = 50;

    /// <summary>Field-level rules: required SKU (50) / name (150), defined valuation method, base UOM.</summary>
    /// <exception cref="StockValidationException">An invariant was violated.</exception>
    public static void EnsureValidFields(string? code, string? name, ValuationMethod valuationMethod, Guid baseUomId)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new StockValidationException(StockErrorCodes.ItemCodeRequired, "Item Code (SKU) is required.");
        }

        if (code.Length > MaxCodeLength)
        {
            throw new StockValidationException(
                StockErrorCodes.ItemCodeTooLong,
                $"Item Code must not exceed {MaxCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StockValidationException(StockErrorCodes.ItemNameRequired, "Item Name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new StockValidationException(
                StockErrorCodes.ItemNameTooLong,
                $"Item Name must not exceed {MaxNameLength} characters.");
        }

        if (!Enum.IsDefined(valuationMethod))
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidValuationMethod,
                $"ValuationMethod must be one of: {string.Join(", ", Enum.GetNames<ValuationMethod>())}.");
        }

        if (baseUomId == Guid.Empty)
        {
            throw new StockValidationException(
                StockErrorCodes.BaseUomRequired,
                "An item must declare a Base UOM (BaseUomId is required).");
        }
    }

    /// <summary>Field-level rules for a unit of measure: code (20) / name (50) and factor &gt; 0.</summary>
    /// <exception cref="StockValidationException">An invariant was violated.</exception>
    public static void EnsureValidUomFields(string? code, string? name, decimal toBaseFactor)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new StockValidationException(StockErrorCodes.UomCodeRequired, "UOM Code is required.");
        }

        if (code.Length > MaxUomCodeLength)
        {
            throw new StockValidationException(
                StockErrorCodes.UomCodeTooLong,
                $"UOM Code must not exceed {MaxUomCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StockValidationException(StockErrorCodes.UomNameRequired, "UOM Name is required.");
        }

        if (name.Length > MaxUomNameLength)
        {
            throw new StockValidationException(
                StockErrorCodes.UomNameTooLong,
                $"UOM Name must not exceed {MaxUomNameLength} characters.");
        }

        if (toBaseFactor <= 0)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidToBaseFactor,
                "ToBaseFactor must be greater than zero (it converts this UOM into the item's Base UOM).");
        }
    }
}
