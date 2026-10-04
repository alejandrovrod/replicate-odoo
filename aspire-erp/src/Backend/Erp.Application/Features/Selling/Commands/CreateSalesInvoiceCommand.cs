using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Commands;

public sealed record SalesInvoiceLineCommandDto(
    Guid ItemId,
    decimal Quantity,
    decimal Rate
);

public sealed record CreateSalesInvoiceCommand(
    Guid CompanyId,
    Guid CustomerId,
    DateOnly PostingDate,
    IReadOnlyList<SalesInvoiceLineCommandDto> Items
) : ICommand<Result<SalesInvoiceDto>>;
