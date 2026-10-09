using Erp.Domain.Entities;
using Erp.Domain.Services;

namespace Erp.Application.Services;

/// <summary>
/// Default <see cref="IBankTransactionRuleEvaluator"/>: account-scoped rules fire only on
/// their own account's lines, global rules on any line; the first predicate hit wins.
/// </summary>
public sealed class BankTransactionRuleEvaluator : IBankTransactionRuleEvaluator
{
    public BankTransactionRule? FindFirstMatch(
        IReadOnlyList<BankTransactionRule> rulesInPriorityOrder,
        BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(rulesInPriorityOrder);
        ArgumentNullException.ThrowIfNull(transaction);

        return rulesInPriorityOrder.FirstOrDefault(rule =>
            (rule.BankAccountId is null || rule.BankAccountId == transaction.BankAccountId)
            && BankRuleMatcher.Evaluate(rule, transaction));
    }
}
