using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Configuration template linking a class of assets to its General Ledger accounts (Task 10.1,
/// plan.md §1 DDL table 1, spec glossary "category → 4 GL accounts"): the Fixed Asset account,
/// the Accumulated Depreciation contra-asset, the Depreciation Expense account and the CWIP
/// account cleared on capitalization.
/// </summary>
/// <remarks>
/// <para>
/// Tenant-scoped: implements <c>ITenantEntity</c> (Constitution Article II.1). Temporal
/// (system-versioned history table, Constitution IV.2), mirroring <see cref="Account"/> and
/// <see cref="Company"/>.
/// </para>
/// <para>
/// ADDITION vs plan DDL: <see cref="GainOnDisposalAccountId"/> and
/// <see cref="LossOnDisposalAccountId"/> (both nullable). The plan's category DDL carries no
/// disposal accounts, but invariant AS-03 (spec §2) books the disposal variance to a
/// gain/loss account - without these links the Task 10.5 disposal voucher cannot balance.
/// Nullable so existing categories stay valid; Block B requires them at disposal time.
/// </para>
/// </remarks>
public class AssetCategory : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company that owns this category and its linked Chart of Accounts.</summary>
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    /// <summary>Display name, e.g. "IT Hardware" (max 100 chars, plan.md §1).</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Balance-sheet account debited with the gross cost on capitalization.</summary>
    public Guid FixedAssetAccountId { get; set; }

    public Account? FixedAssetAccount { get; set; }

    /// <summary>Contra-asset account credited by every periodic depreciation posting (AS-02).</summary>
    public Guid AccumulatedDepreciationAccountId { get; set; }

    public Account? AccumulatedDepreciationAccount { get; set; }

    /// <summary>Expense account debited by every periodic depreciation posting (AS-02).</summary>
    public Guid DepreciationExpenseAccountId { get; set; }

    public Account? DepreciationExpenseAccount { get; set; }

    /// <summary>
    /// Capital Work In Progress account credited on capitalization. NULL means outright purchases
    /// of this category cannot be capitalized through CWIP (Task 10.2 boundary: the capitalize
    /// handler rejects such categories with <c>missing_cwip_account</c> instead of inventing
    /// accounting policy).
    /// </summary>
    public Guid? CwipAccountId { get; set; }

    public Account? CwipAccount { get; set; }

    /// <summary>
    /// Disposal variance account credited when a sale realizes a gain (AS-03). Validated like any
    /// other linked account WHEN SET; required at disposal time (Block B, Task 10.5).
    /// </summary>
    public Guid? GainOnDisposalAccountId { get; set; }

    public Account? GainOnDisposalAccount { get; set; }

    /// <summary>
    /// Disposal variance account debited when a sale or scrap realizes a loss (AS-03).
    /// Validated like any other linked account WHEN SET; required at disposal time (Block B).
    /// </summary>
    public Guid? LossOnDisposalAccountId { get; set; }

    public Account? LossOnDisposalAccount { get; set; }

    /// <summary>Land and other non-wasting assets: no depreciation schedule is generated.</summary>
    public bool IsNonDepreciable { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): EF puts the original value
    /// in the UPDATE ... WHERE clause, so a concurrent change between load and save throws
    /// <c>DbUpdateConcurrencyException</c> instead of silently winning.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
