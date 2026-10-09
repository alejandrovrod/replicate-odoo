using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Selling.Commands;

public sealed record UpdateCustomerCommand(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    string? TaxId,
    decimal? CreditLimit,
    int PaymentTermsDays,
    Guid? ReceivableAccountId,
    bool IsActive,
    byte[] RowVersion,
    string? CustomerType = null,
    string? CustomerGroup = null,
    string? Territory = null,
    string? BillingAddress = null,
    string? Phone = null,
    string? Email = null,
    string? ContactPerson = null,
    string? Website = null,
    string? PaymentTerms = null,
    string? CustomerDetails = null) : ICommand<Result<CustomerDto>>;
