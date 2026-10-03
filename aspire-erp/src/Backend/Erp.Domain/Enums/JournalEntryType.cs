namespace Erp.Domain.Entities;

/// <summary>
/// The economic nature of a manual voucher - plan.md §1 lists it under <c>Erp.Domain/Enums</c>
/// next to <see cref="AccountType"/> and it is persisted as the enum NAME (nvarchar) so history,
/// API payloads and the ubiquitous language all speak the same words.
/// </summary>
/// <remarks>
/// <para>
/// Member set comes from spec.md §1, which defines the Journal Entry as the "Multi-line voucher
/// used for manual accounting adjustments, opening balances, bank transfers, write-offs, and
/// inter-company movements":
/// </para>
/// <list type="bullet">
/// <item><see cref="Standard"/> = the generic manual adjustment / write-off (spec's "manual
/// accounting adjustments" and "write-offs" - a write-off IS an adjustment that debits an expense
/// or loss and credits the asset), also the fallback for inter-company movements until the
/// dedicated multi-company module posts them;</item>
/// <item><see cref="OpeningBalance"/> = balances brought forward when a company starts using the
/// ERP (debit/credit opening positions, never a P&amp;L event);</item>
/// <item><see cref="BankTransfer"/> = money moved between two bank/cash accounts of the SAME
/// company - no P&amp;L impact, pure asset reclassification;</item>
/// <item><see cref="Adjustment"/> = period-end corrections (accruals, prepayments, reclassifications
/// between accounts) that the other three do not describe.</item>
/// </list>
/// <para>
/// <see cref="Standard"/> MUST stay value 0: it is the CLR default of the property, the value a
/// client gets when it omits <c>type</c>, and the column's SQL DEFAULT - so "not set" and
/// "standard" converge on the same stored value (the same convention as <see cref="AccountType.Other"/>).
/// </para>
/// </remarks>
public enum JournalEntryType
{
    /// <summary>Generic manual voucher: adjustments, write-offs, inter-company movements (must remain value 0).</summary>
    Standard,

    /// <summary>Opening balances carried into the system when a company starts its books here.</summary>
    OpeningBalance,

    /// <summary>Transfer between two bank/cash accounts of the same company (no P&amp;L impact).</summary>
    BankTransfer,

    /// <summary>Period-end correction: accruals, prepayments and account reclassifications.</summary>
    Adjustment,
}
