using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="SetSupplierHoldStatusCommand"/>: optimistic-concurrency check plus the
/// aggregate's <see cref="Supplier.SetHoldStatus"/> so HoldType/ReleaseDate stay consistent.
/// </summary>
public sealed class SetSupplierHoldStatusCommandHandler
    : ICommandHandler<SetSupplierHoldStatusCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;

    public SetSupplierHoldStatusCommandHandler(ISupplierRepository suppliers)
    {
        _suppliers = suppliers;
    }

    public async Task<Result<SupplierDto>> HandleAsync(
        SetSupplierHoldStatusCommand request,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result<SupplierDto>.Failure(PurchaseErrorCodes.SupplierNotFound, "Supplier not found.");
        }

        // RowVersion is optional here (a dedicated hold/release toggle): when supplied it is
        // compared-and-swapped, when omitted the toggle applies to the current row.
        if (request.RowVersion is not null
            && (supplier.RowVersion is null || !supplier.RowVersion.AsSpan().SequenceEqual(request.RowVersion)))
        {
            return Result<SupplierDto>.Failure(
                "concurrency_conflict",
                "The supplier was modified by another user.");
        }

        supplier.SetHoldStatus(request.OnHold, request.HoldType, request.ReleaseDate);

        await _suppliers.UpdateAsync(supplier, cancellationToken);

        return Result<SupplierDto>.Success(SupplierDto.From(supplier));
    }
}
