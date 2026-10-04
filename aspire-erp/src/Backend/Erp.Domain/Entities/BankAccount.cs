using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Commercial bank profile linking a bank account number to a liquid asset account in the Chart
/// of Accounts (plan.md §1 DDL literal).
/// </summary>
/// <remarks>
/// System-versioned like Account / Company / Customer: plan.md §1 DDL literal
/// <c>WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.BankAccountHistory))</c>, so account
/// detail changes keep a full history.
/// </remarks>
public sealed class BankAccount : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company that owns this bank account (plan.md §1 FK_BankAccount_Company).</summary>
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    /// <summary>Display name of the account (max 100 chars, plan.md §1).</summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Bank institution name (max 100 chars, plan.md §1).</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>External account number (max 50 chars, plan.md §1).</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency (3 chars, default 'USD', plan.md §1).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Liquid asset account in the Chart of Accounts (plan.md §1 FK_BankAccount_GLAccount).</summary>
    public Guid GLAccountId { get; set; }

    public Account? GLAccount { get; set; }

    /// <summary>Balance at the last reconciliation (decimal(18,4), default 0, plan.md §1).</summary>
    public decimal LastReconciledBalance { get; set; }

    /// <summary>Date of the last reconciliation, if any.</summary>
    public DateOnly? LastReconciledDate { get; set; }

    /// <summary>Inactive accounts cannot receive new statement imports.</summary>
    public bool IsActive { get; set; } = true;
}
