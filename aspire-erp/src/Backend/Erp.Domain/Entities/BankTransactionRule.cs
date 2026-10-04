using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Heuristic condition kinds of the matching engine (plan.md §1 DDL literal:
/// Contains, StartsWith, RegexMatch, AmountEquals). Persisted as the enum NAME (nvarchar),
/// matching the <see cref="BankTransactionStatus"/> precedent.
/// </summary>
public enum RuleConditionType
{
    Contains,
    StartsWith,
    RegexMatch,
    AmountEquals,
}

/// <summary>
/// Configurable heuristic pattern (plan.md §1 DDL) that the rules engine
/// (task 6.3, scenario BN-02) evaluates against unreconciled staging lines.
/// A matching rule marks the transaction <c>Matched</c> and pre-populates the
/// suggestion columns on <see cref="BankTransaction"/>.
/// </summary>
/// <remarks>
/// <c>BankAccountId = null</c> means the rule is GLOBAL (applies to every account of the
/// company); a non-null value scopes it to one account. Rules with
/// <c>AutoCreateVoucher = true</c> are evaluated and REPORTED by the Block B engine but
/// never posted (posting is task 6.5 / Block C).
/// </remarks>
public sealed class BankTransactionRule : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Display name of the rule (max 100 chars, plan.md §1).</summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>Lower values evaluate first; the first matching rule wins per transaction.</summary>
    public int Priority { get; set; } = 1;

    /// <summary>Null = global rule (every account of the company).</summary>
    public Guid? BankAccountId { get; set; }

    public BankAccount? BankAccount { get; set; }

    public RuleConditionType ConditionType { get; set; }

    /// <summary>
    /// Match pattern (max 255 chars): substring / prefix / regex over the statement narrative,
    /// or a decimal amount for <see cref="RuleConditionType.AmountEquals"/>.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Suggested counterparty role (e.g. "Customer"), if the rule names one.</summary>
    public string? TargetPartyType { get; set; }

    /// <summary>Suggested counterparty row id, if the rule names one.</summary>
    public Guid? TargetPartyId { get; set; }

    /// <summary>
    /// Block B evaluates and REPORTS these rules as <c>RequiresVoucherCreation</c> but never
    /// posts a voucher (task 6.5 / Block C owns posting).
    /// </summary>
    public bool AutoCreateVoucher { get; set; }

    /// <summary>Suggested clearing / expense account, if the rule names one.</summary>
    public Guid? TargetExpenseAccountId { get; set; }

    /// <summary>Inactive rules are skipped by the engine.</summary>
    public bool IsActive { get; set; } = true;
}
