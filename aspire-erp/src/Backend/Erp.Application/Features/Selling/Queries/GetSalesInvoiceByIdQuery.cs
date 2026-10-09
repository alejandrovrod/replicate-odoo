using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>Single-invoice read (null = 404 at the API).</summary>
public sealed record GetSalesInvoiceByIdQuery(Guid CompanyId, Guid SalesInvoiceId)
    : IQuery<SalesInvoiceDto?>;
