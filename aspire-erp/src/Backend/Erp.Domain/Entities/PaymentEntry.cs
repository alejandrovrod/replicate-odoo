using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Direction of a payment voucher (spec 05-banking §1: "PaymentEntry"
/// linked to a <see cref="BankAccount"/>). Persisted as the enum NAME (nvarchar),
/// matching the <see cref="StockEntryType"/> precedent.
/// </summary>
public enum PaymentType
{
    /// <summary>Money received from a customer (increases the bank balance).</summary>
    Receive,

    /// <summary>Money paid to a supplier or expense account (decreases the bank balance).</summary>
    Pay,
}

/// <summary>
/// Lifecycle of a payment voucher: created unreconciled, stamped reconciled by the
/// Bank Reconciliation Tool (Block B), which also sets <see cref="ClearanceDate"/> (BN-03).
/// </summary>
public enum PaymentStatus
{
    Unreconciled,
    Reconciled,
}

/// <summary>
/// A payment voucher against a bank account. Allocations to sales invoices are guarded by
/// <see cref="Allocate"/> (task 6.1 anti-overpayment invariant:
/// <c>alreadyAllocated + amount &lt;= invoiceOutstanding</c>).
/// </summary>
/// <remarks>
/// The invoice outstanding balance is taken as a VALUE parameter rather than a navigation
/// because invoice submission lives in the deferred 03-selling scope: the invariant must hold
/// without depending on that pipeline.
/// </remarks>
public sealed class PaymentEntry : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BankAccountId { get; set; }

    public BankAccount? BankAccount { get; set; }

    public PaymentType PaymentType { get; set; }

    /// <summary>Accounting date of the payment.</summary>
    public DateOnly PaymentDate { get; set; }

    /// <summary>Paid amount (decimal(18,4)).</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>External reference (cheque / transfer number), if any.</summary>
    public string? ReferenceNumber { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Unreconciled;

    /// <summary>
    /// Date the payment cleared the bank. Stamped by reconciliation (invariant BN-03, Block B);
    /// null until then.
    /// </summary>
    public DateOnly? ClearanceDate { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): concurrent reconciliation
    /// attempts on the same voucher lose instead of silently winning. Store-generated.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();

    /// <summary>
    /// Guards one allocation step against overpayment (task 6.1): the running total for the
    /// invoice (<paramref name="alreadyAllocated"/> plus <paramref name="amount"/>) must not
    /// exceed <paramref name="invoiceOutstanding"/>. Landing exactly ON the outstanding balance
    /// is allowed.
    /// </summary>
    /// <param name="amount">Amount this step allocates to the invoice.</param>
    /// <param name="invoiceOutstanding">Invoice outstanding balance taken as a value (03-selling deferred).</param>
    /// <param name="alreadyAllocated">Sum previously allocated to the same invoice.</param>
    /// <exception cref="BankingValidationException">
    /// <paramref name="amount"/> is zero or negative (<c>invalid_allocation_amount</c>), or the
    /// step would breach the outstanding cap (<c>over_allocation</c>).
    /// </exception>
    public void Allocate(decimal amount, decimal invoiceOutstanding, decimal alreadyAllocated)
    {
        if (amount <= 0m)
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidAllocationAmount,
                $"Allocation amount must be positive (received {amount:0.####}).");
        }

        if (alreadyAllocated + amount > invoiceOutstanding)
        {
            throw new BankingValidationException(
                BankingErrorCodes.OverAllocation,
                $"Cannot allocate {amount:0.####}: already allocated {alreadyAllocated:0.####} "
                + $"against an outstanding balance of {invoiceOutstanding:0.####}.",
                amount,
                invoiceOutstanding,
                alreadyAllocated);
        }
    }
}
