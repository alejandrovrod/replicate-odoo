namespace Erp.Domain.Entities;

/// <summary>
/// ERPNext parity (<c>hold_type</c> on Supplier): which buying document an <see cref="Supplier"/>
/// hold blocks. <see cref="Supplier.IsBlockedFor"/> maps a requested action to this set.
/// </summary>
/// <remarks>
/// Persisted as <c>INT</c> (spec DDL <c>[HoldType] INT NOT NULL DEFAULT 0</c>) and exposed as the
/// enum NAME on the wire through the global <c>JsonStringEnumConverter</c> ("None", "Invoices",
/// "Payments", "All") - the same split as <see cref="PurchaseOrderStatus"/>.
/// </remarks>
public enum SupplierHoldType
{
    /// <summary>No hold; <see cref="Supplier.OnHold"/> is false.</summary>
    None = 0,

    /// <summary>Blocks purchase invoices (billing) for this supplier.</summary>
    Invoices = 1,

    /// <summary>Blocks payment entries against this supplier.</summary>
    Payments = 2,

    /// <summary>Blocks every buying document (invoices, payments and orders).</summary>
    All = 3,
}
