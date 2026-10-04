using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Creates one heuristic matching rule (task 6.3) for Block C's rule management UI.
/// </summary>
/// <param name="CompanyId">Company that owns the rule.</param>
/// <param name="RuleName">Display name (max 100 chars).</param>
/// <param name="Priority">Lower values evaluate first.</param>
/// <param name="BankAccountId">Null = global rule (every account of the company).</param>
/// <param name="ConditionType">Contains / StartsWith / RegexMatch / AmountEquals.</param>
/// <param name="Pattern">Substring / prefix / regex over the narrative, or a decimal amount.</param>
/// <param name="TargetPartyType">Suggested counterparty role, if any.</param>
/// <param name="TargetPartyId">Suggested counterparty row id, if any.</param>
/// <param name="AutoCreateVoucher">Evaluated and reported by Block B, posted only by Block C.</param>
/// <param name="TargetExpenseAccountId">Suggested clearing / expense account, if any.</param>
/// <param name="IsActive">Inactive rules are skipped by the engine.</param>
public sealed record CreateBankTransactionRuleCommand(
    Guid CompanyId,
    string RuleName,
    int Priority,
    Guid? BankAccountId,
    string ConditionType,
    string Pattern,
    string? TargetPartyType = null,
    Guid? TargetPartyId = null,
    bool AutoCreateVoucher = false,
    Guid? TargetExpenseAccountId = null,
    bool IsActive = true) : ICommand<Result<BankTransactionRuleDto>>;
