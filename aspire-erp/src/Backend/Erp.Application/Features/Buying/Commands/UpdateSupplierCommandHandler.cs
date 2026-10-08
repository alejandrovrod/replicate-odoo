using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

public sealed class UpdateSupplierCommandHandler : ICommandHandler<UpdateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _repository;

    public UpdateSupplierCommandHandler(ISupplierRepository repository)
    {
        _repository = repository;
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

        supplier.Code = request.Code;
        supplier.Name = request.Name;
        supplier.TaxId = request.TaxId ?? string.Empty;
        
        supplier.PaymentTermsDays = request.PaymentTermsDays;
        
        if (request.PayableAccountId.HasValue)
        {
            supplier.DefaultPayableAccountId = request.PayableAccountId.Value;
        }

        supplier.IsActive = request.IsActive;

        await _repository.UpdateAsync(supplier, cancellationToken);

        return Result<SupplierDto>.Success(SupplierDto.From(supplier));
    }
}
