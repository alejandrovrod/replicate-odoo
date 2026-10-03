using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// The five COA root categories (ubiquitous language / value object of Aggregate 2 - see
/// .specify/domain_business_rules_ddd.md 3.2). Persisted as NVARCHAR(20) per plan.md 7.3.
/// </summary>
public enum AccountRootType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense,
}

/// <summary>
/// One node of a company's hierarchical Chart of Accounts (COA). Aggregate root of the General
/// Ledger bounded context (ubiquitous language: "Chart of Accounts", "Group Account",
/// "Leaf / Posting Account" - .specify/domain_business_rules_ddd.md 1 and 3.2).
/// </summary>
/// <remarks>
/// Tenant-scoped: implements <c>ITenantEntity</c> (Constitution Article II.1). The TenantId value
/// is stamped automatically on insert and can never be altered afterwards (Constitution II.4);
/// queries are isolated by AppDbContext's global query filter (Constitution II.3).
/// Validation rules live in <see cref="AccountValidator"/> (pure Domain - Task 2.1).
/// </remarks>
public class Account : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company that owns this Chart of Accounts (a company owns its COA).</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Unique-within-company code, e.g. "1110" (max 50 chars, plan.md 7.3).</summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>Display name (max 150 chars, plan.md 7.3).</summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Asset / Liability / Equity / Income / Expense - children inherit the parent's value (rule 3.2.2).</summary>
    public AccountRootType RootType { get; set; }

    /// <summary>
    /// ERPNext-parity sub-classification (Bank, Cash, Receivable, Payable, COGS, Stock, ...):
    /// NVARCHAR(50) NOT NULL per plan.md §7.3, persisted as the enum NAME (see
    /// <see cref="AccountType"/>). Defaults to <see cref="AccountType.Other"/>; when a client
    /// omits `type`, CreateAccountCommandHandler resolves a per-RootType default instead
    /// (documented on CreateAccountCommand.Type).
    /// </summary>
    public AccountType Type { get; set; } = AccountType.Other;

    /// <summary>
    /// Group Account = non-posting folder (direct postings forbidden); Leaf = posting account.
    /// A leaf is terminal: it can never have children (ubiquitous language 1, invariant 3.2.1).
    /// </summary>
    public bool IsGroup { get; set; }

    /// <summary>Parent in the COA tree; null for a root-level account.</summary>
    public Guid? ParentAccountId { get; set; }

    /// <summary>ISO 4217 currency code (3 chars, plan.md 7.3).</summary>
    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - specs ST-06/BY-06): EF puts
    /// the original value in the UPDATE ... WHERE clause, so a concurrent change made between the
    /// load and the save throws <c>DbUpdateConcurrencyException</c> instead of silently winning.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public Company? Company { get; set; }

    public Account? Parent { get; set; }

    public ICollection<Account> Children { get; set; } = new List<Account>();
}
