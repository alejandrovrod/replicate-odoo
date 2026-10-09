using Erp.Application.Services;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 6.3 contract: the injectable <see cref="IBankTransactionRuleEvaluator"/> returns the
/// first matching rule in caller-provided priority order (or null), honouring account
/// scoping. Pure unit tests, no database (Constitution I.2).
/// </summary>
public sealed class BankTransactionRuleEvaluatorTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly IBankTransactionRuleEvaluator _evaluator = new BankTransactionRuleEvaluator();

    private BankTransaction Line(string description, Guid? accountId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            BankAccountId = accountId ?? _accountId,
            TransactionDate = new DateOnly(2026, 10, 2),
            Deposit = 100m,
            Description = description,
        };

    private static BankTransactionRule Rule(string pattern, RuleConditionType condition, Guid? accountId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            RuleName = "rule",
            Priority = 1,
            BankAccountId = accountId,
            ConditionType = condition,
            Pattern = pattern,
        };

    [Fact]
    public void FindFirstMatch_Hit_ReturnsRule()
    {
        var rule = Rule("STRIPE", RuleConditionType.Contains);

        var hit = _evaluator.FindFirstMatch([rule], Line("STRIPE PAYOUT 123"));

        Assert.Same(rule, hit);
    }

    [Fact]
    public void FindFirstMatch_NoHit_ReturnsNull()
    {
        var hit = _evaluator.FindFirstMatch(
            [Rule("STRIPE", RuleConditionType.Contains)], Line("BANK FEE"));

        Assert.Null(hit);
    }

    [Fact]
    public void FindFirstMatch_FirstInOrderWins()
    {
        var first = Rule("PAY", RuleConditionType.Contains);
        var second = Rule("PAYOUT", RuleConditionType.Contains);

        var hit = _evaluator.FindFirstMatch([first, second], Line("STRIPE PAYOUT"));

        Assert.Same(first, hit);
    }

    [Fact]
    public void FindFirstMatch_ScopedRuleIgnoresForeignAccount()
    {
        var scoped = Rule("FEE", RuleConditionType.Contains, Guid.NewGuid());

        var hit = _evaluator.FindFirstMatch([scoped], Line("BANK FEE", _accountId));

        Assert.Null(hit);
    }

    [Fact]
    public void FindFirstMatch_InvalidRegex_NeverThrowsNorMatches()
    {
        var hit = _evaluator.FindFirstMatch(
            [Rule("([unclosed", RuleConditionType.RegexMatch)], Line("anything"));

        Assert.Null(hit);
    }
}
