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
    byte[] RowVersion) : ICommand<Result<CustomerDto>>;
