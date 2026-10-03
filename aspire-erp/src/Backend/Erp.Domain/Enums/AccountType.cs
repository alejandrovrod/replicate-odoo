namespace Erp.Domain.Entities;

/// <summary>
/// ERPNext-parity sub-classification of an account (the <c>Account.account_type</c> DocType field)
/// - plan.md §1 lists it under <c>Erp.Domain/Enums</c> and §2.2 mandates the physical column
/// <c>Type NVARCHAR(50) NOT NULL</c> ("Bank, Cash, Receivable, Payable, COGS, Stock, etc.").
/// </summary>
/// <remarks>
/// Persisted as the enum NAME (max 50 chars) through AccountConfiguration's
/// <c>HasConversion&lt;string&gt;().HasMaxLength(50)</c>, mirroring how <see cref="AccountRootType"/>
/// is stored, so temporal history rows stay human-readable and match the API contract
/// (Erp.Api serializes enums as strings via JsonStringEnumConverter).
///
/// <para><see cref="Other"/> MUST stay the first member (value 0): it is both the entity-level
/// fallback (Account.Type initializer), the per-RootType default for Asset/Liability
/// (CreateAccountCommandHandler.DefaultTypeFor) and the SQL DEFAULT of the column - so the EF
/// "not set" sentinel (CLR default of the property) and the database default converge on the
/// same stored value.</para>
///
/// Member set: the ERPNext account_type options (Bank, Cash, Receivable, Payable, Stock, COGS,
/// Tax, Equity, Depreciation, RoundOff - RoundOff justified by plan.md §2.1 RoundOffAccountId)
/// plus Revenue/Expense so every RootType has a generic posting classification, plus Other as
/// the documented fallback for accounts without a specific ERPNext type (e.g. group accounts
/// and interim liabilities such as "Stock Received But Not Billed", for which ERPNext leaves
/// account_type blank).
/// </remarks>
public enum AccountType
{
    /// <summary>Fallback: no specific ERPNext account type applies (must remain value 0).</summary>
    Other,

    /// <summary>Bank account (ERPNext account_type "Bank").</summary>
    Bank,

    /// <summary>Cash on hand / cash equivalents (ERPNext account_type "Cash").</summary>
    Cash,

    /// <summary>Asset posted by customer invoices (ERPNext account_type "Receivable").</summary>
    Receivable,

    /// <summary>Liability posted by supplier bills (ERPNext account_type "Payable").</summary>
    Payable,

    /// <summary>Inventory value account (ERPNext account_type "Stock").</summary>
    Stock,

    /// <summary>Cost Of Goods Sold expense (ERPNext account_type "COGS").</summary>
    COGS,

    /// <summary>Tax account (input/output VAT - ERPNext account_type "Tax").</summary>
    Tax,

    /// <summary>Equity account (ERPNext account_type "Equity").</summary>
    Equity,

    /// <summary>Income/revenue account (generic classification for the Income RootType).</summary>
    Revenue,

    /// <summary>Expense account (generic classification for the Expense RootType).</summary>
    Expense,

    /// <summary>Depreciation expense (ERPNext account_type "Depreciation").</summary>
    Depreciation,

    /// <summary>Round-off/adjustment account (ERPNext account_type "Round Off").</summary>
    RoundOff,
}
