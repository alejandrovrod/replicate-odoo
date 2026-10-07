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
