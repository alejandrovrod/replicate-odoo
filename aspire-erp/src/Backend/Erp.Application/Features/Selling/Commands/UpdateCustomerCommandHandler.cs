using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Commands;

public sealed class UpdateCustomerCommandHandler : ICommandHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _repository;

    public UpdateCustomerCommandHandler(ICustomerRepository repository)
    {
        _repository = repository;
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
