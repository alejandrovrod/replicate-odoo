using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Domain.Inventory;

namespace AssetHub.Application.Interfaces;

public interface IInventoryPostingService
{
    Task<InventoryTransaction> PostTransactionAsync(
        Guid warehouseId,
        Guid catalogItemId,
        decimal quantity,
        decimal unitCost,
        string type,
        string reason,
        string idempotencyKey,
        Guid? maintenanceOrderId = null,
        CancellationToken cancellationToken = default);

    Task<bool> EnsureInventoryModeIsActiveAsync(Guid tenantId, CancellationToken cancellationToken);
}
