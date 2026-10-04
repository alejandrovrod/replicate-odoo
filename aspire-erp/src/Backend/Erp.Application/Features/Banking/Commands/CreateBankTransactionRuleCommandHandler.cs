using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Creates one <see cref="BankTransactionRule"/> (task 6.3) for Block C's rule management UI.
/// </summary>
/// <remarks>
/// Rejects unknown condition names, blank patterns, unparsable AmountEquals patterns and
/// regexes that do not compile (<c>invalid_rule_pattern</c> - the matcher itself never
/// throws, so a bad pattern is caught HERE instead of silently never matching). A scoped
/// rule names an account that must exist and belong to the company
/// (<c>bank_account_not_found</c>).
/// </remarks>
public sealed class CreateBankTransactionRuleCommandHandler
    : ICommandHandler<CreateBankTransactionRuleCommand, Result<BankTransactionRuleDto>>
{
    private readonly Domain.Repositories.IBankRepository _bank;

    public CreateBankTransactionRuleCommandHandler(Domain.Repositories.IBankRepository bank)
    {
        _bank = bank;
    }

    public async Task<Result<BankTransactionRuleDto>> HandleAsync(
        CreateBankTransactionRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<RuleConditionType>(command.ConditionType, ignoreCase: true, out var condition))
        {
            return Result<BankTransactionRuleDto>.Failure(
                BankingErrorCodes.InvalidRulePattern,
                $"Unknown rule condition type '{command.ConditionType}' "
                + "(expected Contains, StartsWith, RegexMatch or AmountEquals).");
        }

        if (string.IsNullOrWhiteSpace(command.Pattern))
        {
            return Result<BankTransactionRuleDto>.Failure(
                BankingErrorCodes.InvalidRulePattern,
                "Rule pattern must be a non-empty string.");
        }

        var patternError = ValidatePattern(condition, command.Pattern);
        if (patternError is not null)
        {
            return Result<BankTransactionRuleDto>.Failure(
                BankingErrorCodes.InvalidRulePattern, patternError);
        }

        Guid? accountId = null;
        Guid tenantId;
        if (command.BankAccountId is not null)
        {
            var account = await _bank.GetAccountByIdAsync(command.BankAccountId.Value, cancellationToken);
            if (account is null || account.CompanyId != command.CompanyId)
            {
                return Result<BankTransactionRuleDto>.Failure(
                    BankingErrorCodes.BankAccountNotFound,
                    $"Bank account '{command.BankAccountId}' not found in company '{command.CompanyId}'.");
            }

            accountId = account.Id;
            tenantId = account.TenantId;
        }
        else
        {
            // Global rule: the tenant comes from any account of the company. A company with no
            // bank account cannot own rules yet (bank_account_not_found).
            var siblings = await _bank.GetAccountsByCompanyAsync(command.CompanyId, cancellationToken);
            var first = siblings.FirstOrDefault();
            if (first is null)
            {
                return Result<BankTransactionRuleDto>.Failure(
                    BankingErrorCodes.BankAccountNotFound,
                    $"Company '{command.CompanyId}' has no bank account in this tenant.");
            }

            tenantId = first.TenantId;
        }

        var rule = new BankTransactionRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = command.CompanyId,
            RuleName = command.RuleName,
            Priority = command.Priority,
            BankAccountId = accountId,
            ConditionType = condition,
            Pattern = command.Pattern,
            TargetPartyType = command.TargetPartyType,
            TargetPartyId = command.TargetPartyId,
            AutoCreateVoucher = command.AutoCreateVoucher,
            TargetExpenseAccountId = command.TargetExpenseAccountId,
            IsActive = command.IsActive,
        };

        await _bank.AddRuleAsync(rule, cancellationToken);

        return Result<BankTransactionRuleDto>.Success(BankTransactionRuleDto.From(rule));
    }

    private static string? ValidatePattern(RuleConditionType condition, string pattern) =>
        condition switch
        {
            RuleConditionType.RegexMatch => ValidateRegex(pattern),
            RuleConditionType.AmountEquals => decimal.TryParse(pattern, out _)
                ? null
                : $"AmountEquals rule pattern must be a decimal amount (received '{pattern}').",
            _ => null,
        };

    private static string? ValidateRegex(string pattern)
    {
        try
        {
            _ = new System.Text.RegularExpressions.Regex(pattern);
            return null;
        }
        catch (ArgumentException)
        {
            return $"RegexMatch rule pattern is not a valid regular expression (received '{pattern}').";
        }
    }
}
