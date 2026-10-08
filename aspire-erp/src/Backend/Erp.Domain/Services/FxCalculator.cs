using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Services;

public static class FxCalculator
{
    /// <summary>
    /// Computes the realized gain/loss for a payment allocation slice.
    /// Positive = Gain (Credit), Negative = Loss (Debit).
    /// </summary>
    public static decimal RealizedPerSlice(decimal allocFC, decimal settlementRate, decimal invoiceRate)
    {
        // Example logic based on standard FX math:
        // Settlement CC = allocFC * settlementRate
        // Invoice CC = allocFC * invoiceRate
        // Diff = Settlement CC - Invoice CC
        
        return Math.Round(allocFC * (settlementRate - invoiceRate), 6);
    }

    /// <summary>
    /// Computes the unrealized gain/loss for a revaluation line.
    /// Returns null if the difference is within the allowed rounding loss (dust) and no line is needed.
    /// </summary>
    public static ExchangeRateRevaluationLine? UnrealizedPerLine(
        decimal balanceFC, 
        decimal balanceCC, 
        decimal newRate, 
        decimal allowance,
        Guid accountId,
        Guid currencyId)
    {
        var newBalanceCC = Math.Round(balanceFC * newRate, 6);
        var diff = newBalanceCC - balanceCC;

        // If difference is essentially zero or just dust within allowance
        if (Math.Abs(diff) <= allowance)
        {
            return null; // Drop row
        }

        return new ExchangeRateRevaluationLine
        {
            AccountId = accountId,
            CurrencyId = currencyId,
            BalanceInForeignCurrency = balanceFC,
            BalanceInBaseCurrency = balanceCC,
            CurrentExchangeRate = balanceFC == 0 ? 0 : Math.Round(balanceCC / balanceFC, 6),
            NewExchangeRate = newRate,
            GainLossAmount = diff
        };
    }

    /// <summary>
    /// Asserts that the double entry is balanced within standard rounding tolerance (±0.0001).
    /// </summary>
    public static void AssertBalanced(decimal totalDebits, decimal totalCredits)
    {
        if (Math.Abs(totalDebits - totalCredits) > 0.0001m)
        {
            // Assuming DoubleEntryImbalance is part of another shared exception class, 
            // for simplicity throwing FxValidationException here
            throw new FxValidationException("double_entry_imbalance", $"Debits ({totalDebits}) and Credits ({totalCredits}) do not match.");
        }
    }
}
