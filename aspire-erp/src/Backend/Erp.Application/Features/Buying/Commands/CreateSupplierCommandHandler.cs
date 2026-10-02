using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="CreateSupplierCommand"/>: pure Domain validation (PurchaseValidator), the
/// duplicate-code-per-tenant rule and persistence (Task 4.1 acceptance).
/// </summary>
public sealed class CreateSupplierCommandHandler : ICommandHandler<CreateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;

    public CreateSupplierCommandHandler(ISupplierRepository suppliers)
    {
        _suppliers = suppliers;
    }

    public async Task<Result<SupplierDto>> HandleAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PurchaseValidator.EnsureValidSupplierFields(command.Code, command.Name);

            var code = command.Code.Trim();

            if (await _suppliers.ExistsCodeAsync(code, cancellationToken))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.DuplicateSupplierCode,
                    $"Supplier code '{code}' already exists in this tenant.");
            }

            var supplier = new Supplier
            {
                // Id is generated here so tests can inspect the entity before it is persisted.
                Id = Guid.NewGuid(),
                Code = code,
                Name = command.Name.Trim(),
                IsActive = command.IsActive,

                // TenantId is intentionally NOT set: AppDbContext stamps CurrentTenantId on insert
                // and throws when no tenant context exists (Constitution Article II.4, fail closed).
            };

            await _suppliers.AddAsync(supplier, cancellationToken);

            return Result<SupplierDto>.Success(SupplierDto.From(supplier));
        }
        catch (PurchaseValidationException ex)
        {
            return Result<SupplierDto>.Failure(ex.Code, ex.Message);
        }
    }
}
