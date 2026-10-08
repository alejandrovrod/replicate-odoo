using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Payments.Queries;

/// <summary>Company payment vouchers, newest first (optional document-status/type filters).</summary>
public sealed record GetPaymentsQuery(
    Guid CompanyId,
    int Page = 1,
    int PageSize = 50,
    PaymentDocumentStatus? Status = null,
    PaymentType? PaymentType = null) : IQuery<PagedResult<PaymentEntryDto>>;

/// <summary>One voucher with its allocation slices, or null.</summary>
public sealed record GetPaymentDetailQuery(
    Guid CompanyId,
    Guid PaymentId) : IQuery<PaymentEntryDetailDto?>;

/// <summary>Open receivables of one customer (the Receive allocation grid).</summary>
public sealed record GetOutstandingSalesInvoicesQuery(
    Guid CompanyId,
    Guid CustomerId) : IQuery<IReadOnlyList<OutstandingInvoiceDto>>;

/// <summary>Open payables of one supplier (the Pay allocation grid).</summary>
public sealed record GetOutstandingPurchaseInvoicesQuery(
    Guid CompanyId,
    Guid SupplierId) : IQuery<IReadOnlyList<OutstandingInvoiceDto>>;
