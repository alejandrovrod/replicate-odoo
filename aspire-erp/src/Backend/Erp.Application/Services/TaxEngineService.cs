using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Services;

/// <summary>
/// In-memory <see cref="ITaxEngineService"/> (tax-engine Fase 2, T2): sequential,
/// order-dependent computation. Amounts are SIGNED: an <c>Add</c> row follows the base
/// sign (positive on normal documents, negative on returns); a <c>Deduct</c> row carries
/// the opposite sign, so <c>GrandTotal = NetBase + Σ TaxAmount</c> holds in every
/// direction and the return of a document is its exact mirror.
/// </summary>
/// <remarks>
/// <para><c>OnPreviousRow</c> compounds on the running total (base + all previous signed
/// rows): IEPS 8% on 1000 → 80, then IVA 16% on (1000 + 80) = 172.80 (TAX-G3).
/// <c>FixedAmount</c> books its amount verbatim (signed by charge type); the rate is
/// ignored.</para>
/// <para>Row ORDER is significant for cascades and is preserved verbatim (template line
/// order, then manual rows). Validation (rate range, known enums) throws
/// <see cref="TaxValidationException"/> - account existence stays in the handlers, which
/// own data access.</para>
/// </remarks>
public sealed class TaxEngineService : ITaxEngineService
{
    public TaxCalculationResult Calculate(
        decimal netBase,
        IReadOnlyList<TaxRowInput> rows,
        int decimals)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (decimals is < 0 or > 6)
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxCalculationBasis,
                $"Rounding decimals must be between 0 and 6 (received {decimals}).");
        }

        var results = new List<TaxRowResult>(rows.Count);
        var runningTotal = netBase;

        foreach (var row in rows)
        {
            EnsureValidRow(row);

            decimal baseAmount;
            decimal raw;
            if (row.CalculationBasis == TaxCalculationBasis.FixedAmount)
            {
                baseAmount = 0m;
                raw = Round(row.FixedAmount, decimals);
            }
            else
            {
                baseAmount = row.CalculationBasis == TaxCalculationBasis.OnPreviousRow
                    ? runningTotal
                    : netBase;
                raw = Round(baseAmount * row.Rate / 100m, decimals);
            }

            // Deduct rows oppose the base (retentions shrink the payable); Add rows follow it
            // (normal: positive tax; return with negative base: negative tax - the mirror).
            var signed = row.ChargeType == TaxChargeType.Deduct ? -raw : raw;

            results.Add(new TaxRowResult(baseAmount, signed, row.ChargeType, row.CalculationBasis, row.Rate));
            runningTotal += signed;
        }

        var addTotal = results.Where(r => r.ChargeType == TaxChargeType.Add).Sum(r => r.TaxAmount);
        var deductTotal = results.Where(r => r.ChargeType == TaxChargeType.Deduct).Sum(r => Math.Abs(r.TaxAmount));
        // Signed sum (Add follows the base, Deduct opposes it): the document total is always
        // NetBase + TaxTotal, and a return mirrors its original row by row.
        var taxTotal = results.Sum(r => r.TaxAmount);

        return new TaxCalculationResult(
            results,
            addTotal,
            deductTotal,
            taxTotal,
            netBase + taxTotal);
    }

    private static void EnsureValidRow(TaxRowInput row)
    {
        if (row.ChargeType is not TaxChargeType.Add and not TaxChargeType.Deduct)
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxChargeType,
                $"Unknown tax charge type '{(int)row.ChargeType}'.");
        }

        if (row.CalculationBasis is not TaxCalculationBasis.OnNetTotal
            and not TaxCalculationBasis.OnPreviousRow
            and not TaxCalculationBasis.FixedAmount)
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxCalculationBasis,
                $"Unknown tax calculation basis '{(int)row.CalculationBasis}'.");
        }

        if (row.CalculationBasis != TaxCalculationBasis.FixedAmount
            && (row.Rate < 0 || row.Rate > 100))
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxRate,
                $"Tax rate must be between 0 and 100 (received {row.Rate:0.####}).");
        }
    }

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
