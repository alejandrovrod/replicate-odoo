using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Commands;

public sealed class UpdateCustomerCommandHandler : ICommandHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _repository;
    private readonly ICustomFieldValidator? _customFields;

    public UpdateCustomerCommandHandler(ICustomerRepository repository, ICustomFieldValidator? customFields = null)
    {
        _repository = repository;
        _customFields = customFields;
    }

    public async Task<Result<CustomerDto>> HandleAsync(UpdateCustomerCommand request, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (customer is null || customer.CompanyId != request.CompanyId)
        {
            return Result<CustomerDto>.Failure(SellingErrorCodes.CustomerNotFound, "Customer not found.");
        }

        if (!customer.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CustomerDto>.Failure(
                "concurrency_conflict",
                "The customer was modified by another user.");
        }

        if (customer.CustomerCode != request.Code)
        {
            var exists = await _repository.ExistsCodeAsync(request.CompanyId, request.Code, cancellationToken);
            if (exists)
            {
                return Result<CustomerDto>.Failure(
                    SellingErrorCodes.DuplicateCustomerCode,
                    $"Customer with code '{request.Code}' already exists.");
            }
        }

        customer.CustomerCode = request.Code;
        customer.CustomerName = request.Name;
        customer.TaxId = request.TaxId;
        customer.CreditLimit = request.CreditLimit ?? 0;

        // ERPNext-parity profile fields: null means "not sent" (keep stored value);
        // an explicit value (including empty) overwrites, trimmed like on create.
        if (request.CustomerType is not null)
        {
            customer.CustomerType = string.IsNullOrWhiteSpace(request.CustomerType) ? "Company" : request.CustomerType.Trim();
        }
        if (request.CustomerGroup is not null) customer.CustomerGroup = request.CustomerGroup.Trim();
        if (request.Territory is not null) customer.Territory = request.Territory.Trim();
        if (request.BillingAddress is not null) customer.BillingAddress = request.BillingAddress.Trim();
        if (request.Phone is not null) customer.Phone = request.Phone.Trim();
        if (request.Email is not null) customer.Email = request.Email.Trim();
        if (request.ContactPerson is not null) customer.ContactPerson = request.ContactPerson.Trim();
        if (request.Website is not null) customer.Website = request.Website.Trim();
        if (request.PaymentTerms is not null) customer.PaymentTerms = request.PaymentTerms.Trim();
        if (request.CustomerDetails is not null) customer.CustomerDetails = request.CustomerDetails.Trim();

        // Extensibility Fase 1: null means "not sent" (keep stored value), like the profile
        // fields above; an explicit value (including empty, which clears) is validated first.
        if (request.CustomData is not null)
        {
            if (_customFields is not null)
            {
                var customError = await _customFields.ValidateAsync("Customer", request.CustomData, cancellationToken);
                if (customError is not null)
                {
                    return Result<CustomerDto>.Failure(customError.Code, customError.Message);
                }
            }

            customer.CustomData = request.CustomData;
        }

        customer.PaymentTermsDays = request.PaymentTermsDays;
        
        if (request.ReceivableAccountId.HasValue)
        {
            customer.DefaultReceivableAccountId = request.ReceivableAccountId.Value;
        }

        customer.IsActive = request.IsActive;

        await _repository.UpdateAsync(customer, cancellationToken);

        return Result<CustomerDto>.Success(CustomerDto.From(customer));
    }
}
