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
    private readonly ICurrencyRepository _currencies;

    public CreateSupplierCommandHandler(ISupplierRepository suppliers, ICurrencyRepository currencies)
    {
        _suppliers = suppliers;
        _currencies = currencies;
    }

    public async Task<Result<SupplierDto>> HandleAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PurchaseValidator.EnsureValidSupplierFields(command.Code, command.Name, command.PaymentTermsDays);

            if (command.CurrencyId.HasValue
                && await _currencies.GetByIdAsync(command.CurrencyId.Value, cancellationToken) is null)
            {
                throw new PurchaseValidationException(
                    CurrencyErrorCodes.CurrencyNotFound,
                    $"Currency '{command.CurrencyId.Value}' was not found.");
            }

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
                TaxId = command.TaxId.Trim(),
                DefaultPayableAccountId = command.DefaultPayableAccountId,
                CurrencyId = command.CurrencyId,
                PaymentTermsDays = command.PaymentTermsDays,
                OutstandingAmount = 0.0000m,
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
