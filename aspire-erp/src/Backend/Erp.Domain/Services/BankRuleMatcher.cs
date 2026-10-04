using Erp.Domain.Entities;

namespace Erp.Domain.Services;

/// <summary>
/// Heuristic matching engine (task 6.3, scenario BN-02): pure Domain (no EF, no NuGet -
/// Constitution Article I.2), unit-tested without a database like
/// <see cref="CreditControlEvaluator"/>.
/// </summary>
/// <remarks>
/// <b>plan.md §2 VERBATIM semantics.</b> Contains / StartsWith are case-insensitive;
/// RegexMatch is case-insensitive (<c>RegexOptions.IgnoreCase</c>); AmountEquals parses
/// <c>rule.Pattern</c> as a decimal and compares it to |Deposit - Withdrawal| with exact
/// decimal equality. An invalid regex or an unparsable amount pattern is a NO-MATCH
/// (returns false), never a throw - a misconfigured rule must not break the whole run.
/// </remarks>
public sealed class BankRuleMatcher
{
    /// <summary>
    /// Evaluates one rule against one staging transaction (first matching rule wins per
    /// transaction in the command handler; priority ordering lives there, not here).
    /// </summary>
    public static bool Evaluate(BankTransactionRule rule, BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(transaction);

        return rule.ConditionType switch
        {
            RuleConditionType.Contains => transaction.Description.Contains(
                rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleConditionType.StartsWith => transaction.Description.StartsWith(
                rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleConditionType.RegexMatch => EvaluateRegex(rule.Pattern, transaction.Description),
            RuleConditionType.AmountEquals => decimal.TryParse(rule.Pattern, out var amount)
                && Math.Abs(transaction.Deposit - transaction.Withdrawal) == amount,
            _ => false,
        };
    }

    private static bool EvaluateRegex(string pattern, string description)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                description,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            // Invalid regex in a stored rule: no-match, never a throw (see class remarks).
            return false;
        }
    }
}
