using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Commands;

public sealed record SalesInvoiceLineCommandDto(
    Guid ItemId,
    decimal Quantity,
    decimal Rate
);

/// <summary>
/// One "Taxes and Charges" input row (module 17): only the liability account and the rate
/// travel on the wire - <c>TaxAmount</c> is ALWAYS recomputed server-side from the discounted
/// net base (ERPNext "On Net Total" default), so clients can never book arbitrary tax money.
/// </summary>
public sealed record SalesInvoiceTaxCommandDto(
    Guid AccountId,
    decimal Rate
);

public sealed record CreateSalesInvoiceCommand(
    Guid CompanyId,
    Guid CustomerId,
    DateOnly PostingDate,
    IReadOnlyList<SalesInvoiceLineCommandDto> Items,
    // Module 17: global discount (ERPNext "apply on Net Total") + tax rows. All defaulted so
    // existing callers (controller model binding, POS, tests) keep compiling unchanged.
    decimal DiscountPercentage = 0m,
    decimal DiscountAmount = 0m,
    IReadOnlyList<SalesInvoiceTaxCommandDto>? Taxes = null,
    // Module 18: credit-note mode + provenance. Stock flags make the UpdateStock return path
    // reachable (spec 18 §3: re-enter inventory when true).
    bool IsReturn = false,
    Guid? ReturnAgainstId = null,
    bool UpdateStock = false,
    Guid? SourceWarehouseId = null
) : ICommand<Result<SalesInvoiceDto>>;
