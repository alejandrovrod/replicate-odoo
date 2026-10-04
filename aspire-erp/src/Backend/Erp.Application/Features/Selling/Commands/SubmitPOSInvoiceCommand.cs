using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Commands;

public sealed record POSPaymentLine(
    string ModeOfPayment, // "Cash" or "Card"
    decimal Amount
);

public sealed record POSInvoiceLine(
    Guid ItemId,
    decimal Quantity,
    decimal Rate
);

public sealed record SubmitPOSInvoiceCommand(
    Guid CompanyId,
    Guid CustomerId,
    Guid POSProfileId,
    DateOnly PostingDate,
    IReadOnlyList<POSInvoiceLine> Items,
    IReadOnlyList<POSPaymentLine> Payments
) : ICommand<Result<SalesInvoiceDto>>;
