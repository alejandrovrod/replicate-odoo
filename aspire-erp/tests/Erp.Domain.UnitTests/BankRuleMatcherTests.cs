using Erp.Domain.Entities;
using Erp.Domain.Services;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 6.3 acceptance: <see cref="BankRuleMatcher.Evaluate"/> implements plan.md §2 VERBATIM -
/// Contains / StartsWith case-insensitive, RegexMatch case-insensitive, AmountEquals on
/// |Deposit - Withdrawal| with exact decimal equality. A misconfigured rule (invalid regex,
/// unparsable amount) is a no-match (false), never a throw.
/// </summary>
/// <remarks>Pure C#, no EF Core and no database (Constitution I.2/I.3).</remarks>
public sealed class BankRuleMatcherTests
{
    private static BankTransactionRule Rule(RuleConditionType condition, string pattern) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            RuleName = "Test rule",
            ConditionType = condition,
            Pattern = pattern,
        };

    private static BankTransaction Transaction(string description, decimal deposit, decimal withdrawal) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            BankAccountId = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 2),
            Deposit = deposit,
            Withdrawal = withdrawal,
            Description = description,
        };

    // ----------------------------------------------------------------------- Contains

    [Fact]
    public void Evaluate_ContainsSubstring_Matches()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.Contains, "STRIPE PAYOUT"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_ContainsIsCaseInsensitive_Matches()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.Contains, "stripe payout"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_ContainsAbsentSubstring_DoesNotMatch()
    {
        Assert.False(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.Contains, "ADYEN"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    // --------------------------------------------------------------------- StartsWith

    [Fact]
    public void Evaluate_StartsWithPrefix_Matches()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.StartsWith, "STRIPE"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_StartsWithIsCaseInsensitive_Matches()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.StartsWith, "stripe"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_StartsWithNonPrefix_DoesNotMatch()
    {
        Assert.False(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.StartsWith, "PAYOUT"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    // --------------------------------------------------------------------- RegexMatch

    [Fact]
    public void Evaluate_RegexMatch_Matches()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.RegexMatch, @"REF #\d+"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_RegexMatchIsCaseInsensitive_Matches()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.RegexMatch, @"stripe payout ref #\d+"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_RegexNonMatch_DoesNotMatch()
    {
        Assert.False(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.RegexMatch, @"^FEE "),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_InvalidRegex_ReturnsFalseInsteadOfThrowing()
    {
        Assert.False(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.RegexMatch, "([unclosed"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    // ------------------------------------------------------------------- AmountEquals

    [Fact]
    public void Evaluate_AmountEqualsDeposit_Matches()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.AmountEquals, "5400"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_AmountEqualsWithdrawalSide_MatchesAbsoluteValue()
    {
        Assert.True(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.AmountEquals, "15"),
            Transaction("MONTHLY BANK FEE", 0m, 15m)));
    }

    [Fact]
    public void Evaluate_AmountEqualsOffByCent_DoesNotMatch()
    {
        Assert.False(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.AmountEquals, "5400.01"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    [Fact]
    public void Evaluate_AmountEqualsUnparsablePattern_ReturnsFalseInsteadOfThrowing()
    {
        Assert.False(BankRuleMatcher.Evaluate(
            Rule(RuleConditionType.AmountEquals, "not-a-number"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }

    // -------------------------------------------------------------------------- guard

    [Fact]
    public void Evaluate_UnknownConditionType_ReturnsFalse()
    {
        Assert.False(BankRuleMatcher.Evaluate(
            Rule((RuleConditionType)999, "anything"),
            Transaction("STRIPE PAYOUT REF #98234", 5400m, 0m)));
    }
}
