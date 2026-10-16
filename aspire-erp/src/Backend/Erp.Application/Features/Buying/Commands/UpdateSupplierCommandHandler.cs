using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="UpdateSupplierCommand"/>: optimistic-concurrency check, duplicate code guard,
/// group/account validation and the governance flags (module buying-suppliers spec §1).
/// </summary>
public sealed class UpdateSupplierCommandHandler : ICommandHandler<UpdateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _repository;
    private readonly ISupplierGroupRepository? _supplierGroups;
    private readonly ICompanyRepository? _companies;
    private readonly IAccountRepository? _accounts;
    private readonly ICustomFieldValidator? _customFields;

    public UpdateSupplierCommandHandler(
        ISupplierRepository repository,
        ISupplierGroupRepository? supplierGroups = null,
        ICompanyRepository? companies = null,
        IAccountRepository? accounts = null,
        ICustomFieldValidator? customFields = null)
    {
        _repository = repository;
        _supplierGroups = supplierGroups;
        _companies = companies;
        _accounts = accounts;
        _customFields = customFields;
    }

    public async Task<Result<SupplierDto>> HandleAsync(UpdateSupplierCommand request, CancellationToken cancellationToken = default)
    {
        var supplier = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result<SupplierDto>.Failure("supplier_not_found", "Supplier not found.");
        }

        if (supplier.RowVersion is null || !supplier.RowVersion.AsSpan().SequenceEqual(request.RowVersion))
        {
            return Result<SupplierDto>.Failure(
                "concurrency_conflict",
                "The supplier was modified by another user.");
        }

        try
        {
            if (supplier.Code != request.Code)
            {
                var exists = await _repository.ExistsCodeAsync(request.Code, cancellationToken);
                if (exists)
                {
                    return Result<SupplierDto>.Failure(
                        PurchaseErrorCodes.DuplicateSupplierCode,
                        $"Supplier with code '{request.Code}' already exists.");
                }
            }

            PurchaseValidator.EnsureValidSupplierFields(request.Code, request.Name, request.PaymentTermsDays);

            if (request.SupplierGroupId.HasValue
                && _supplierGroups is not null
                && await _supplierGroups.GetByIdAsync(request.SupplierGroupId.Value, cancellationToken) is null)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierGroupNotFound,
                    $"Supplier Group '{request.SupplierGroupId.Value}' was not found in this tenant.");
            }

            await SupplierCompanyAccountRules.ValidateAsync(
                request.CompanyAccounts, _companies, _accounts, cancellationToken);
        }
        catch (PurchaseValidationException ex)
        {
            return Result<SupplierDto>.Failure(ex.Code, ex.Message);
        }

        supplier.Code = request.Code;
        supplier.Name = request.Name;
        supplier.TaxId = request.TaxId ?? string.Empty;

        supplier.PaymentTermsDays = request.PaymentTermsDays;

        supplier.SupplierGroupId = request.SupplierGroupId;
        supplier.PaymentTermsTemplateId = request.PaymentTermsTemplateId;
        supplier.DefaultPriceListId = request.DefaultPriceListId;
        supplier.IsFrozen = request.IsFrozen;

        // Governance: a single entry point keeps HoldType/ReleaseDate consistent with OnHold.
        supplier.SetHoldStatus(request.OnHold, request.HoldType, request.ReleaseDate);

        // Extensibility Fase 1: null means "not sent" (keep); an explicit value is validated.
        if (request.CustomData is not null)
        {
            if (_customFields is not null)
            {
                var customError = await _customFields.ValidateAsync("Supplier", request.CustomData, cancellationToken);
                if (customError is not null)
                {
                    return Result<SupplierDto>.Failure(customError.Code, customError.Message);
                }
            }

            supplier.CustomData = request.CustomData;
        }

        if (request.PayableAccountId.HasValue)
        {
            supplier.DefaultPayableAccountId = request.PayableAccountId.Value;
        }

        supplier.IsActive = request.IsActive;

        // The caller sends the FULL account map: drop the ones no longer present, then upsert.
        if (request.CompanyAccounts is not null)
        {
            var keep = request.CompanyAccounts.Select(a => a.CompanyId).ToHashSet();
            foreach (var stale in supplier.Accounts.Where(a => !keep.Contains(a.CompanyId)).ToList())
            {
                supplier.Accounts.Remove(stale);
            }

            foreach (var account in request.CompanyAccounts)
            {
                supplier.SetCompanyAccount(account.CompanyId, account.PayableAccountId, account.AdvanceAccountId);
            }
        }

        await _repository.UpdateAsync(supplier, cancellationToken);

        return Result<SupplierDto>.Success(SupplierDto.From(supplier));
    }
}
