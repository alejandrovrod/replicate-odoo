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

    /// <summary>
    /// ERPNext parity: bank-to-bank (or bank-to-cash) transfer between two accounts of the
    /// company (<see cref="PaymentEntry.PaidFromAccountId"/> → <see cref="PaymentEntry.PaidToAccountId"/>).
    /// Draft capture is supported; settlement posting is deferred (see tasks.md Phase 6 notes).
    /// </summary>
    InternalTransfer,
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
/// Counterparty kind of a payment voucher (spec R-12 invariant PE-06): <c>Receive</c> settles
/// a <c>Customer</c>, <c>Pay</c> settles a <c>Supplier</c> or an <c>Employee</c> advance.
/// Persisted as the enum NAME.
/// </summary>
public enum PaymentPartyType
{
    Customer,
    Supplier,
    Employee,
}

/// <summary>
/// Submittable lifecycle of a payment voucher (spec R-12 invariant PE-04): <c>Draft</c> (no GL
/// impact) → <c>Submitted</c> (GL settled, gapless <c>VoucherNo</c>) → <c>Cancelled</c>
/// (compensating reversal). Independent from <see cref="PaymentStatus"/>, which tracks BANK
/// reconciliation (BN-03), not submission.
/// </summary>
public enum PaymentDocumentStatus
{
    Draft,
    Submitted,
    Cancelled,
}

/// <summary>
/// A payment voucher against a bank account (spec R-12: closes the A/R and A/P loop left open
/// by 05-banking staging). Allocations to invoices are guarded by <see cref="Allocate"/>
/// (task 6.1 anti-overpayment invariant PE-02, extended to purchase bills:
/// <c>alreadyAllocated + amount &lt;= invoiceOutstanding</c>), and the header obeys the
/// conservation law PE-03 (<see cref="EnsureConservation"/>).
/// </summary>
/// <remarks>
/// The invoice outstanding balance is taken as a VALUE parameter rather than a navigation
/// because the invariant must hold without depending on the invoice pipelines.
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

    /// <summary>Counterparty kind (PE-06): Customer for Receive, Supplier for Pay.</summary>
    public PaymentPartyType PartyType { get; set; }

    /// <summary>Counterparty id (Customer.Id or Supplier.Id, per <see cref="PartyType"/>).</summary>
    public Guid PartyId { get; set; }

    /// <summary>ERPNext parity (<c>party_name</c>): counterparty display name snapshot.</summary>
    public string PartyName { get; set; } = string.Empty;

    /// <summary>ERPNext parity (<c>mode_of_payment</c>): Cash, Bank Draft, Wire Transfer, ...</summary>
    public string ModeOfPayment { get; set; } = string.Empty;

    /// <summary>
    /// ERPNext parity (<c>paid_from</c>): source GL account of the money. On Receive/Pay legs
    /// this mirrors the bank profile's GL account; on Internal Transfer it is the explicit
    /// source account. Null until set.
    /// </summary>
    public Guid? PaidFromAccountId { get; set; }

    /// <summary>ERPNext parity: ISO currency of the paid-from account (default USD).</summary>
    public string PaidFromAccountCurrency { get; set; } = "USD";

    /// <summary>
    /// ERPNext parity (<c>paid_to</c>): destination GL account of the money. Null until set.
    /// </summary>
    public Guid? PaidToAccountId { get; set; }

    /// <summary>ERPNext parity: ISO currency of the paid-to account (default USD).</summary>
    public string PaidToAccountCurrency { get; set; } = "USD";

    /// <summary>
    /// ERPNext parity (<c>source_exchange_rate</c>): transaction → paid-from currency rate.
    /// 1 when single-currency; refreshed from the FX catalog on submit.
    /// </summary>
    public decimal SourceExchangeRate { get; set; } = 1m;

    /// <summary>ERPNext parity (<c>base_paid_amount</c>): PaidAmount in company currency.</summary>
    public decimal BasePaidAmount { get; set; }

    /// <summary>
    /// ERPNext parity (<c>received_amount</c>): amount landing on the destination side, in
    /// paid-to currency. Defaults to PaidAmount on create; refreshed on submit.
    /// </summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>
    /// ERPNext parity (<c>target_exchange_rate</c>): transaction → paid-to currency rate.
    /// 1 when single-currency; refreshed from the FX catalog on submit.
    /// </summary>
    public decimal TargetExchangeRate { get; set; } = 1m;

    /// <summary>ERPNext parity (<c>base_received_amount</c>): ReceivedAmount in company currency.</summary>
    public decimal BaseReceivedAmount { get; set; }

    /// <summary>
    /// ERPNext parity: sum of allocation slices, snapshotted on submit (PE-03: equals
    /// PaidAmount − UnallocatedAmount).
    /// </summary>
    public decimal TotalAllocatedAmount { get; set; }

    /// <summary>
    /// ERPNext parity (<c>difference_amount</c>): PaidAmount − TotalAllocatedAmount −
    /// UnallocatedAmount. Zero on a conserved voucher (PE-03); nonzero flags a data fix-up.
    /// </summary>
    public decimal DifferenceAmount { get; set; }

    /// <summary>ERPNext parity (<c>reference_date</c>): date of the external reference, if any.</summary>
    public DateOnly? ReferenceDate { get; set; }

    /// <summary>ERPNext parity (<c>cost_center</c>): optional cost center dimension.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>ERPNext parity (<c>project</c>): optional project dimension.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>ERPNext parity: free-text remarks on the voucher.</summary>
    public string Remarks { get; set; } = string.Empty;

    /// <summary>
    /// Gapless fiscal number (<c>PAY-YYYY-NNNNN</c>, Constitution III.4): assigned inside the
    /// posting transaction on first submit; empty while Draft.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    /// <summary>Accounting date of the payment.</summary>
    public DateOnly PaymentDate { get; set; }

    /// <summary>Paid amount (decimal(18,4), strictly positive).</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// Money moved without invoice backing (PE-03: <c>PaidAmount − Σ allocations</c>, never
    /// negative): a customer or supplier advance kept on the voucher for future allocation.
    /// </summary>
    public decimal UnallocatedAmount { get; set; }

    /// <summary>External reference (cheque / transfer number), if any.</summary>
    public string? ReferenceNumber { get; set; }

    public Guid? TransactionCurrencyId { get; set; }
    public Currency? TransactionCurrency { get; set; }
    public decimal SettlementExchangeRate { get; set; } = 1m;


    /// <summary>Submittable lifecycle (PE-04): Draft → Submitted → Cancelled.</summary>
    public PaymentDocumentStatus DocumentStatus { get; set; } = PaymentDocumentStatus.Draft;

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

    /// <summary>
    /// Payment conservation law (spec R-12 invariant PE-03): the paid amount must equal the
    /// allocated slices plus the unallocated advance, and the advance is never negative.
    /// </summary>
    /// <exception cref="BankingValidationException">
    /// <c>invalid_paid_amount</c> when the paid amount is not positive, or
    /// <c>payment_conservation_violated</c> when the slices do not add up.
    /// </exception>
    public void EnsureConservation(IEnumerable<decimal> allocatedAmounts, decimal unallocatedAmount)
    {
        if (PaidAmount <= 0m)
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidPaidAmount,
                $"Paid amount must be positive (received {PaidAmount:0.####}).");
        }

        var allocated = 0m;
        foreach (var slice in allocatedAmounts)
        {
            allocated += slice;
        }

        if (unallocatedAmount < 0m || allocated + unallocatedAmount != PaidAmount)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentConservationViolated,
                $"Paid amount {PaidAmount:0.####} must equal allocated {allocated:0.####} "
                + $"plus unallocated {unallocatedAmount:0.####} (unallocated is never negative).");
        }
    }

    /// <summary>
    /// Directional consistency (spec R-12 invariant PE-06, extended for ERPNext parity):
    /// Receive ↔ Customer, Pay ↔ Supplier/Employee, InternalTransfer ↔ any party (the
    /// transfer carries no counterparty - PaidFrom/PaidTo own the legs). Called by the
    /// application guard before any row exists.
    /// </summary>
    /// <exception cref="BankingValidationException"><c>payment_party_mismatch</c>.</exception>
    public void EnsureDirection()
    {
        var consistent = (PaymentType == PaymentType.Receive && PartyType == PaymentPartyType.Customer)
            || (PaymentType == PaymentType.Pay && (PartyType == PaymentPartyType.Supplier || PartyType == PaymentPartyType.Employee))
            || PaymentType == PaymentType.InternalTransfer;

        if (!consistent)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentPartyMismatch,
                $"PaymentType '{PaymentType}' requires PartyType "
                + $"'{(PaymentType == PaymentType.Receive ? PaymentPartyType.Customer : PaymentPartyType.Supplier)}' "
                + $"but was '{PartyType}'.");
        }
    }

    /// <summary>
    /// Lifecycle transition Draft → Submitted (spec R-12 invariant PE-04). The gapless
    /// <see cref="VoucherNo"/> is assigned by the handler inside the numbering lock, before or
    /// after this call - the guard only owns the state move.
    /// </summary>
    /// <exception cref="BankingValidationException"><c>payment_invalid_transition</c>.</exception>
    public void Submit()
    {
        if (DocumentStatus != PaymentDocumentStatus.Draft)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentInvalidTransition,
                $"A payment voucher can only be submitted from Draft (was '{DocumentStatus}').");
        }

        DocumentStatus = PaymentDocumentStatus.Submitted;
    }

    /// <summary>
    /// Lifecycle transition Submitted → Cancelled (spec R-12 invariant PE-05). The ledger
    /// reversal is appended by the handler; reconciled vouchers (<see cref="ClearanceDate"/> set,
    /// BN-06) cannot cancel until un-reconciled.
    /// </summary>
    /// <exception cref="BankingValidationException">
    /// <c>payment_already_cancelled</c> when already cancelled,
    /// <c>payment_reconciled_cannot_cancel</c> when reconciled, or
    /// <c>payment_invalid_transition</c> from any other status.
    /// </exception>
    public void Cancel()
    {
        if (DocumentStatus == PaymentDocumentStatus.Cancelled)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentAlreadyCancelled,
                $"Payment voucher '{VoucherNo}' is already cancelled.");
        }

        if (DocumentStatus != PaymentDocumentStatus.Submitted)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentInvalidTransition,
                $"A payment voucher can only be cancelled from Submitted (was '{DocumentStatus}').");
        }

        if (ClearanceDate.HasValue)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentReconciledCannotCancel,
                $"Payment voucher '{VoucherNo}' is reconciled (clearance {ClearanceDate:yyyy-MM-dd}): "
                + "un-reconcile it before cancelling.");
        }

        DocumentStatus = PaymentDocumentStatus.Cancelled;
    }
}
