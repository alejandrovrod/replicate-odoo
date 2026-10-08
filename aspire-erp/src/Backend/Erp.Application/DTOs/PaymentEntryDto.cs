using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One allocation slice payload of a payment voucher (spec R-12).</summary>
public sealed record PaymentAllocationDto(
    Guid Id,
    Guid? SalesInvoiceId,
    Guid? PurchaseInvoiceId,
    decimal AllocatedAmount)
{
    public static PaymentAllocationDto From(PaymentAllocation allocation) =>
        new(
            allocation.Id,
            allocation.SalesInvoiceId,
            allocation.PurchaseInvoiceId,
            allocation.AllocatedAmount);
}

/// <summary>Payment voucher payload returned by GET/POST /api/v1/payment-entries (spec R-12).</summary>
public sealed record PaymentEntryDto(
    Guid Id,
    Guid CompanyId,
    string VoucherNo,
    PaymentType PaymentType,
    PaymentPartyType PartyType,
    Guid PartyId,
    Guid BankAccountId,
    DateOnly PaymentDate,
    decimal PaidAmount,
    decimal UnallocatedAmount,
    string? ReferenceNumber,
    PaymentDocumentStatus DocumentStatus,
    PaymentStatus Status,
    DateOnly? ClearanceDate,
    byte[]? RowVersion = null)
{
    public static PaymentEntryDto From(PaymentEntry payment) =>
        new(
            payment.Id,
            payment.CompanyId,
            payment.VoucherNo,
            payment.PaymentType,
            payment.PartyType,
            payment.PartyId,
            payment.BankAccountId,
            payment.PaymentDate,
            payment.PaidAmount,
            payment.UnallocatedAmount,
            payment.ReferenceNumber,
            payment.DocumentStatus,
            payment.Status,
            payment.ClearanceDate,
            payment.RowVersion);
}

/// <summary>Payment voucher detail: the header plus its allocation slices.</summary>
public sealed record PaymentEntryDetailDto(
    PaymentEntryDto Payment,
    IReadOnlyList<PaymentAllocationDto> Allocations);

/// <summary>One open invoice row of the allocation source grid (spec R-12 §4).</summary>
public sealed record OutstandingInvoiceDto(
    Guid Id,
    string Number,
    DateOnly PostingDate,
    DateOnly DueDate,
    decimal GrandTotal,
    decimal OutstandingAmount,
    string Status);
