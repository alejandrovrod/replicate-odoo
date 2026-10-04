using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Executes <see cref="ApplyMatchingRulesCommand"/>: loads the unreconciled staging lines and
/// the active rules, then the first matching rule wins per transaction -&gt; Status = Matched
/// plus the rule's suggestion columns (task 6.3, scenario BN-02).
/// </summary>
/// <remarks>
/// <para><b>Precedence.</b> Rules arrive from
/// <see cref="IBankRepository.GetActiveRulesAsync"/> ordered account-scoped first, then
/// global, priority ascending inside each group. On a company-wide run (no account scope)
/// an account-scoped rule fires ONLY on its own account's lines; global rules fire on any
/// line. Inactive rules never reach the handler (the repository filters them).</para>
/// <para><b>No-match</b> lines stay Unreconciled with NULL suggestions. Rules with
/// <c>AutoCreateVoucher = true</c> are evaluated and reported as
/// <c>RequiresVoucherCreation</c> in the outcome - the engine posts NOTHING (task 6.5 /
/// Block C).</para>
/// <para><b>Atomicity.</b> The whole run commits inside ONE transaction; a RowVersion race
/// (scenario BN-07) rolls everything back and surfaces
/// <c>concurrency_conflict</c>.</para>
/// </remarks>
public sealed class ApplyMatchingRulesCommandHandler
    : ICommandHandler<ApplyMatchingRulesCommand, Result<RuleMatchSummary>>
{
    private readonly IBankRepository _bank;

    public ApplyMatchingRulesCommandHandler(IBankRepository bank)
    {
        _bank = bank;
    }

    public async Task<Result<RuleMatchSummary>> HandleAsync(
        ApplyMatchingRulesCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _bank.ExecuteInTransactionAsync(async token =>
            {
                var transactions = await _bank.GetUnreconciledTransactionsAsync(
                    command.CompanyId, command.BankAccountId, token);
                var rules = await _bank.GetActiveRulesAsync(
                    command.CompanyId, command.BankAccountId, token);

                var outcomes = new List<RuleMatchOutcome>(transactions.Count);
                var matched = 0;

                foreach (var transaction in transactions)
                {
                    var firing = rules.FirstOrDefault(rule =>
                        (rule.BankAccountId is null || rule.BankAccountId == transaction.BankAccountId)
                        && BankRuleMatcher.Evaluate(rule, transaction));

                    if (firing is null)
                    {
                        outcomes.Add(new RuleMatchOutcome(transaction.Id, false, null, null, false));
                        continue;
                    }

                    transaction.Status = BankTransactionStatus.Matched;
                    transaction.SuggestedPartyType = firing.TargetPartyType;
                    transaction.SuggestedPartyId = firing.TargetPartyId;
                    transaction.SuggestedAccountId = firing.TargetExpenseAccountId;

                    await _bank.UpdateTransactionAsync(transaction, token);
                    matched++;

                    outcomes.Add(new RuleMatchOutcome(
                        transaction.Id,
                        true,
                        firing.Id,
                        firing.RuleName,
                        firing.AutoCreateVoucher));
                }

                return Result<RuleMatchSummary>.Success(new RuleMatchSummary(matched, outcomes));
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<RuleMatchSummary>.Failure(ex.Code, ex.Message);
        }
    }
}
