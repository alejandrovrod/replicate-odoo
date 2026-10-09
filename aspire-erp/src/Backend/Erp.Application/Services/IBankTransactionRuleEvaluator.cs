using Erp.Domain.Entities;

namespace Erp.Application.Services;

/// <summary>
/// Heuristic rules engine entry point (task 6.3): given the active rules in evaluation order
/// (account-scoped first, then global, priority ascending inside each group) returns the
/// first rule matching a staging transaction, or null when nothing fires.
/// </summary>
/// <remarks>
/// Hand-rolled CQRS support service (decision C2, no MediatR): the pure predicate lives in
/// Domain (<see cref="Domain.Services.BankRuleMatcher"/>); this contract owns the
/// first-match-wins traversal so it is unit-testable and injectable behind
/// <c>ISender</c>-dispatched handlers.
/// </remarks>
public interface IBankTransactionRuleEvaluator
{
    /// <param name="rulesInPriorityOrder">Active rules, already ordered by the caller.</param>
    /// <param name="transaction">Staging line under evaluation.</param>
    /// <returns>The first matching rule, or null when nothing fires.</returns>
    BankTransactionRule? FindFirstMatch(
        IReadOnlyList<BankTransactionRule> rulesInPriorityOrder,
        BankTransaction transaction);
}
