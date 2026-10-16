using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure creation-time rules for landed cost vouchers (Fase 4, T1). Data-dependent checks
/// (receipt status, account postability) live in the handlers/services, which own repos.
/// </summary>
public static class LandedCostValidator
{
    /// <summary>
    /// A voucher needs at least one receipt and at least one positive charge (LCV-G4 shape).
    /// </summary>
    /// <exception cref="LandedCostValidationException">An invariant was violated.</exception>
    public static void EnsureCreatable(IReadOnlyCollection<Guid> receiptIds)
    {
        if (receiptIds is null || receiptIds.Count == 0)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                "A landed cost voucher must reference at least one purchase receipt.");
        }

        if (receiptIds.Distinct().Count() != receiptIds.Count)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                "A landed cost voucher cannot reference the same purchase receipt twice.");
        }
    }

    /// <summary>
    /// Charge shape rules: non-blank description, strictly positive amount, an account.
    /// </summary>
    /// <exception cref="LandedCostValidationException">An invariant was violated.</exception>
    public static void EnsureValidCharge(string description, decimal amount, Guid expenseAccountId, int index)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.ChargesRequired,
                $"Charge row {index + 1} needs a description (e.g. 'Ocean freight').");
        }

        if (amount <= 0m)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.ChargesRequired,
                $"Charge '{description}' must be greater than zero (received {amount:0.####}).");
        }

        if (expenseAccountId == Guid.Empty)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.ExpenseAccountRequired,
                $"Charge '{description}' must reference a bridge/expense GL account.");
        }
    }

    /// <summary>
    /// Only Submitted receipts absorb landed costs (LCV-G4): Draft rows have no posted
    /// value yet, Cancelled rows were already reversed out of the books.
    /// </summary>
    /// <exception cref="LandedCostValidationException">The receipt cannot absorb costs.</exception>
    public static void EnsureSubmittableReceipt(PurchaseReceipt receipt)
    {
        if (receipt.Status != PurchaseReceiptStatus.Submitted)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                $"Purchase receipt '{receipt.VoucherNo}' is '{receipt.Status}': only Submitted "
                + "receipts absorb landed costs.");
        }
    }

    /// <summary>Lifecycle transitions: Draft → Submitted → Cancelled only.</summary>
    /// <exception cref="LandedCostValidationException">The transition is illegal.</exception>
    public static void EnsureSubmittable(DocumentStatus status, string voucherNo)
    {
        if (status != DocumentStatus.Draft)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.InvalidStatusTransition,
                $"Landed cost voucher '{voucherNo}' must be Draft to submit (is '{status}').");
        }
    }

    /// <summary>Lifecycle transitions: Draft → Submitted → Cancelled only.</summary>
    /// <exception cref="LandedCostValidationException">The transition is illegal.</exception>
    public static void EnsureCancellable(DocumentStatus status, string voucherNo)
    {
        if (status != DocumentStatus.Submitted)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.InvalidStatusTransition,
                $"Landed cost voucher '{voucherNo}' must be Submitted to cancel (is '{status}').");
        }
    }
}
