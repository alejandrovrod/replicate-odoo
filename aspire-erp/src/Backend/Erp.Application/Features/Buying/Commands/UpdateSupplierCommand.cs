using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Buying.Commands;

public sealed record UpdateSupplierCommand(
    Guid Id,
    string Code,
    string Name,
    string? TaxId,
    int PaymentTermsDays,
    Guid? PayableAccountId,
    bool IsActive,
    byte[] RowVersion) : ICommand<Result<SupplierDto>>;
