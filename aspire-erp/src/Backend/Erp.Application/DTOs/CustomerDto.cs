using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Customer payload returned by GET/POST /api/v1/customers (Task 5.1).</summary>
/// <remarks>
/// <c>Code</c>/<c>Name</c> keep the generic master-data names of <see cref="SupplierDto"/> and
/// <see cref="AccountDto"/> (the wire contract is `code`, `name`); everything else mirrors the
/// plan.md §1 columns so the credit fields (creditLimit, bypassCreditLimitCheck,
/// outstandingAmount) round-trip verbatim for Task 5.3/5.5.
/// </remarks>
public sealed record CustomerDto(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    string TaxId,
    Guid? DefaultReceivableAccountId,
    decimal CreditLimit,
    bool BypassCreditLimitCheck,
    Guid? CurrencyId,
    int PaymentTermsDays,
    decimal OutstandingAmount,
    bool IsActive,
    byte[]? RowVersion = null)
{
    public static CustomerDto From(Customer customer) =>
        new(
            customer.Id,
            customer.CompanyId,
            customer.CustomerCode,
            customer.CustomerName,
            customer.TaxId,
            customer.DefaultReceivableAccountId,
            customer.CreditLimit,
            customer.BypassCreditLimitCheck,
            customer.CurrencyId,
            customer.PaymentTermsDays,
            customer.OutstandingAmount,
            customer.IsActive,
            customer.RowVersion);
}
