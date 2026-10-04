using Erp.Application.Common;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Runs the heuristic rules engine over unreconciled staging lines (task 6.3, scenario BN-02).
/// </summary>
/// <param name="CompanyId">Company that owns the transactions and rules.</param>
/// <param name="BankAccountId">Optional account scope; null runs every account of the company.</param>
public sealed record ApplyMatchingRulesCommand(
    Guid CompanyId,
    Guid? BankAccountId = null) : ICommand<Result<RuleMatchSummary>>;

/// <summary>
/// Outcome of one rule run: how many lines matched plus the per-transaction detail.
/// </summary>
/// <param name="MatchedCount">Transactions moved Unreconciled -&gt; Matched.</param>
/// <param name="Outcomes">One entry per candidate transaction (matched or not).</param>
public sealed record RuleMatchSummary(
    int MatchedCount,
    IReadOnlyList<RuleMatchOutcome> Outcomes);

/// <summary>
/// Per-transaction detail of a rule run.
/// </summary>
/// <param name="BankTransactionId">Staging line the entry describes.</param>
/// <param name="Matched">True when a rule fired and the line is now Matched.</param>
/// <param name="RuleId">Firing rule, when <paramref name="Matched"/> is true.</param>
/// <param name="RuleName">Firing rule name, when <paramref name="Matched"/> is true.</param>
/// <param name="RequiresVoucherCreation">
/// True when the firing rule has <c>AutoCreateVoucher = true</c>: Block B evaluates and reports
/// it but posts NOTHING (task 6.5 / Block C owns voucher creation).
/// </param>
public sealed record RuleMatchOutcome(
    Guid BankTransactionId,
    bool Matched,
    Guid? RuleId,
    string? RuleName,
    bool RequiresVoucherCreation);
