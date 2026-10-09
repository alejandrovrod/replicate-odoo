using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One allocation slice payload of a payment voucher (spec R-12).</summary>
public sealed record PaymentAllocationDto(
    Guid Id,
    Guid? SalesInvoiceId,
    Guid? PurchaseInvoiceId,
    decimal AllocatedAmount,
    string ReferenceDocumentType = "",
    Guid? ReferenceDocumentId = null,
    decimal TotalAmount = 0m,
    decimal OutstandingAmount = 0m,
    decimal ExchangeRate = 1m)
{
    public static PaymentAllocationDto From(PaymentAllocation allocation) =>
        new(
            allocation.Id,
            allocation.SalesInvoiceId,
            allocation.PurchaseInvoiceId,
            allocation.AllocatedAmount,
            allocation.ReferenceDocumentType,
            allocation.ReferenceDocumentId,
            allocation.TotalAmount,
            allocation.OutstandingAmount,
            allocation.ExchangeRate);
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
    byte[]? RowVersion = null,
    string PartyName = "",
    string ModeOfPayment = "",
    Guid? PaidFromAccountId = null,
    string PaidFromAccountCurrency = "USD",
    Guid? PaidToAccountId = null,
    string PaidToAccountCurrency = "USD",
    decimal SourceExchangeRate = 1m,
    decimal BasePaidAmount = 0m,
    decimal ReceivedAmount = 0m,
    decimal TargetExchangeRate = 1m,
    decimal BaseReceivedAmount = 0m,
    decimal TotalAllocatedAmount = 0m,
    decimal DifferenceAmount = 0m,
    DateOnly? ReferenceDate = null,
    Guid? CostCenterId = null,
    Guid? ProjectId = null,
    string Remarks = "")
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
            payment.RowVersion,
            payment.PartyName,
            payment.ModeOfPayment,
            payment.PaidFromAccountId,
            payment.PaidFromAccountCurrency,
            payment.PaidToAccountId,
            payment.PaidToAccountCurrency,
            payment.SourceExchangeRate,
            payment.BasePaidAmount,
            payment.ReceivedAmount,
            payment.TargetExchangeRate,
            payment.BaseReceivedAmount,
            payment.TotalAllocatedAmount,
            payment.DifferenceAmount,
            payment.ReferenceDate,
            payment.CostCenterId,
            payment.ProjectId,
            payment.Remarks);
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
