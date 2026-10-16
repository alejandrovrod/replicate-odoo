using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Selling.Commands;

public sealed record SalesInvoiceLineCommandDto(
    Guid ItemId,
    decimal Quantity,
    decimal Rate
);

/// <summary>
/// One "Taxes and Charges" input row (module 17 + tax-engine Fase 2): the account and the
/// computation shape travel on the wire - <c>TaxAmount</c> is ALWAYS recomputed server-side
/// by <c>ITaxEngineService</c>, so clients can never book arbitrary tax money. Charge and
/// basis default to the module-17 behavior (Add on the discounted net base).
/// </summary>
public sealed record SalesInvoiceTaxCommandDto(
    Guid AccountId,
    decimal Rate,
    TaxChargeType ChargeType = TaxChargeType.Add,
    TaxCalculationBasis CalculationBasis = TaxCalculationBasis.OnNetTotal,
    decimal FixedAmount = 0m
);

/// <summary>
/// One advance-cross input (sales-invoice ERPNext parity): <c>AllocatedAmount</c> of a
/// submitted customer payment (<see cref="PaymentEntry.UnallocatedAmount"/>) to cross
/// against the new invoice. Validated against the live payment at creation AND revalidated
/// at submit (the balance can move between Draft and submit).
/// </summary>
public sealed record SalesInvoiceAdvanceCommandDto(
    Guid PaymentEntryId,
    decimal AllocatedAmount
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
    Guid? SourceWarehouseId = null,
    // Tax-engine Fase 2: optional template expansion (template lines first, then manual
    // Taxes rows). The server re-resolves and recomputes everything - the template is a
    // shortcut, never trusted money.
    Guid? TaxTemplateId = null,
    // ERPNext parity (advances allocation): advance slices crossed against the Draft.
    // Persisted immediately as PaymentAllocation rows; settled (payment balances moved)
    // at submit. Rejected on credit notes.
    IReadOnlyList<SalesInvoiceAdvanceCommandDto>? Advances = null
) : ICommand<Result<SalesInvoiceDto>>;
