using Erp.Domain.Entities;

namespace Erp.Application.Services;

/// <summary>One tax row fed into the engine (template line or manual invoice row).</summary>
public sealed record TaxRowInput(
    decimal Rate,
    TaxChargeType ChargeType = TaxChargeType.Add,
    TaxCalculationBasis CalculationBasis = TaxCalculationBasis.OnNetTotal,
    decimal FixedAmount = 0m);

/// <summary>One computed row: the base it ran on and its signed amount.</summary>
public sealed record TaxRowResult(
    decimal BaseAmount,
    decimal TaxAmount,
    TaxChargeType ChargeType,
    TaxCalculationBasis CalculationBasis,
    decimal Rate);

/// <summary>Deterministic result of one engine run (all amounts in document currency).</summary>
public sealed record TaxCalculationResult(
    IReadOnlyList<TaxRowResult> Rows,
    /// <summary>Signed Add sum (follows the base sign: negative on returns).</summary>
    decimal AddTotal,
    /// <summary>Absolute Deduct sum (always ≥ 0, informational for retentions).</summary>
    decimal DeductTotal,
    /// <summary>Signed tax sum: AddTotal − sign(base) × DeductTotal. The document total is Net + TaxTotal.</summary>
    decimal TaxTotal,
    decimal GrandTotal);

/// <summary>
/// Deterministic multi-tier tax engine (tax-engine Fase 2, gap D2): transferred taxes
/// (<c>Add</c>), fiscal retentions (<c>Deduct</c>) and compound cascades
/// (<c>OnPreviousRow</c>), computed purely in memory. Both invoice flows share it; rounding
/// decimals travel as a parameter (2 = sales, 4 = purchase) so each flow keeps its legacy
/// precision.
/// </summary>
public interface ITaxEngineService
{
    TaxCalculationResult Calculate(
        decimal netBase,
        IReadOnlyList<TaxRowInput> rows,
        int decimals);
}
