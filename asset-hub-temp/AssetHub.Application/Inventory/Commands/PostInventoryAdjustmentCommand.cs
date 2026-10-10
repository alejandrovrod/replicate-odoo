using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Inventory.Commands;

public class PostInventoryAdjustmentCommand : IRequest<Guid>
{
    public Guid WarehouseId { get; set; }
    public Guid CatalogItemId { get; set; }
    public decimal Quantity { get; set; } // Negative for out, positive for in
    public decimal UnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class PostInventoryAdjustmentCommandHandler : IRequestHandler<PostInventoryAdjustmentCommand, Guid>
{
    private readonly IInventoryPostingService _postingService;
    private readonly ITenantDbContext _dbContext;

    public PostInventoryAdjustmentCommandHandler(IInventoryPostingService postingService, ITenantDbContext dbContext)
    {
        _postingService = postingService;
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(PostInventoryAdjustmentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new DomainException("validation_error", "IdempotencyKey is required.");
            
        var transaction = await _postingService.PostTransactionAsync(
            warehouseId: request.WarehouseId,
            catalogItemId: request.CatalogItemId,
            quantity: request.Quantity,
            unitCost: request.UnitCost,
            type: "Adjustment",
            reason: request.Reason,
            idempotencyKey: request.IdempotencyKey,
            maintenanceOrderId: null,
            cancellationToken: cancellationToken
        );

        await _dbContext.SaveChangesAsync(cancellationToken);

        return transaction.Id;
    }
}
