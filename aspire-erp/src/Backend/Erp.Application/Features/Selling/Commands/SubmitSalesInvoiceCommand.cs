using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Commands;

public sealed record SubmitSalesInvoiceCommand(
    Guid CompanyId,
    Guid SalesInvoiceId
) : ICommand<Result<SalesInvoiceDto>>;
