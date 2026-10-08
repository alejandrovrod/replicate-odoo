using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Payments.Commands;

/// <summary>One allocation slice of a create/update payment draft (invoice references are IDs).</summary>
public sealed record PaymentAllocationInput(
    Guid? SalesInvoiceId,
    Guid? PurchaseInvoiceId,
    decimal AllocatedAmount);

/// <summary>
/// Creates one payment voucher in <c>Draft</c> state (spec R-12: zero GL impact until submit).
/// Allocations are validated line by line (PE-02 against the live outstanding, PE-03
/// conservation, PE-06 direction) before anything is persisted.
/// </summary>
public sealed record CreatePaymentEntryCommand(
    Guid CompanyId,
    PaymentType PaymentType,
    PaymentPartyType PartyType,
    Guid PartyId,
    Guid BankAccountId,
    DateOnly PaymentDate,
    decimal PaidAmount,
    string? ReferenceNumber,
    IReadOnlyList<PaymentAllocationInput> Allocations) : ICommand<Result<PaymentEntryDto>>;
