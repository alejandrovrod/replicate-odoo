using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="CreateSupplierGroupCommand"/>: pure Domain field validation, the
/// duplicate-code-per-tenant rule and persistence (module buying-suppliers, spec §1 gap D1).
/// </summary>
public sealed class CreateSupplierGroupCommandHandler
    : ICommandHandler<CreateSupplierGroupCommand, Result<SupplierGroupDto>>
{
    private readonly ISupplierGroupRepository _groups;

    public CreateSupplierGroupCommandHandler(ISupplierGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task<Result<SupplierGroupDto>> HandleAsync(
        CreateSupplierGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PurchaseValidator.EnsureValidSupplierGroupFields(command.Code, command.Name);

            var code = command.Code.Trim();

            if (await _groups.ExistsCodeAsync(code, cancellationToken))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.DuplicateSupplierGroupCode,
                    $"Supplier group code '{code}' already exists in this tenant.");
            }

            var group = new SupplierGroup
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = command.Name.Trim(),
                IsActive = command.IsActive,
                // TenantId is intentionally NOT set: AppDbContext stamps it on insert.
            };

            await _groups.AddAsync(group, cancellationToken);

            return Result<SupplierGroupDto>.Success(SupplierGroupDto.From(group));
        }
        catch (PurchaseValidationException ex)
        {
            return Result<SupplierGroupDto>.Failure(ex.Code, ex.Message);
        }
    }
}
