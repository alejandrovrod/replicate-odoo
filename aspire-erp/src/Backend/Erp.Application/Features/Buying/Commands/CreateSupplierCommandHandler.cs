using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="CreateSupplierCommand"/>: pure Domain validation (PurchaseValidator), the
/// duplicate-code-per-tenant rule, the group/multi-company governance wiring and persistence
/// (Task 4.1 acceptance + module buying-suppliers spec §1).
/// </summary>
public sealed class CreateSupplierCommandHandler : ICommandHandler<CreateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;
    private readonly ICurrencyRepository _currencies;
    private readonly ISupplierGroupRepository? _supplierGroups;
    private readonly ICompanyRepository? _companies;
    private readonly IAccountRepository? _accounts;
    private readonly ICustomFieldValidator? _customFields;

    public CreateSupplierCommandHandler(
        ISupplierRepository suppliers,
        ICurrencyRepository currencies,
        ISupplierGroupRepository? supplierGroups = null,
        ICompanyRepository? companies = null,
        IAccountRepository? accounts = null,
        ICustomFieldValidator? customFields = null)
    {
        _suppliers = suppliers;
        _currencies = currencies;
        _supplierGroups = supplierGroups;
        _companies = companies;
        _accounts = accounts;
        _customFields = customFields;
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

            if (command.SupplierGroupId.HasValue
                && _supplierGroups is not null
                && await _supplierGroups.GetByIdAsync(command.SupplierGroupId.Value, cancellationToken) is null)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierGroupNotFound,
                    $"Supplier Group '{command.SupplierGroupId.Value}' was not found in this tenant.");
            }

            await SupplierCompanyAccountRules.ValidateAsync(
                command.CompanyAccounts, _companies, _accounts, cancellationToken);

            var code = command.Code.Trim();

            if (await _suppliers.ExistsCodeAsync(code, cancellationToken))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.DuplicateSupplierCode,
                    $"Supplier code '{code}' already exists in this tenant.");
            }

            // Extensibility Fase 1 (gap D2): validated BEFORE the insert; skipped only when no
            // validator is wired (legacy direct construction) - DI always provides it.
            if (_customFields is not null)
            {
                var customError = await _customFields.ValidateAsync("Supplier", command.CustomData, cancellationToken);
                if (customError is not null)
                {
                    throw new PurchaseValidationException(customError.Code, customError.Message);
                }
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
                CustomData = command.CustomData,
                SupplierGroupId = command.SupplierGroupId,
                PaymentTermsTemplateId = command.PaymentTermsTemplateId,
                DefaultPriceListId = command.DefaultPriceListId,

                // TenantId is intentionally NOT set: AppDbContext stamps CurrentTenantId on insert
                // and throws when no tenant context exists (Constitution Article II.4, fail closed).
            };

            if (command.OnHold)
            {
                supplier.SetHoldStatus(true, command.HoldType, command.ReleaseDate);
            }

            if (command.CompanyAccounts is not null)
            {
                foreach (var account in command.CompanyAccounts)
                {
                    supplier.SetCompanyAccount(account.CompanyId, account.PayableAccountId, account.AdvanceAccountId);
                }
            }

            await _suppliers.AddAsync(supplier, cancellationToken);

            return Result<SupplierDto>.Success(SupplierDto.From(supplier));
        }
        catch (PurchaseValidationException ex)
        {
            return Result<SupplierDto>.Failure(ex.Code, ex.Message);
        }
    }
}
