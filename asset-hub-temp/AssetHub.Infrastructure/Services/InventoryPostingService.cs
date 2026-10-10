using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Inventory;
using AssetHub.Domain.Exceptions;

namespace AssetHub.Infrastructure.Services;

public class InventoryPostingService : IInventoryPostingService
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public InventoryPostingService(
        ITenantDbContext dbContext,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<bool> EnsureInventoryModeIsActiveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.TenantInventorySettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
            
        if (settings == null)
            return false;

        return settings.OperatingMode == InventoryOperatingMode.Internal ||
               settings.OperatingMode == InventoryOperatingMode.Hybrid;
    }

    public async Task<InventoryTransaction> PostTransactionAsync(
        Guid warehouseId,
        Guid catalogItemId,
        decimal quantity,
        decimal unitCost,
        string type,
        string reason,
        string idempotencyKey,
        Guid? maintenanceOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;
        
        // 1. Ensure idempotency
        var existingTx = await _dbContext.InventoryTransactions
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IdempotencyKey == idempotencyKey, cancellationToken);
            
        if (existingTx != null)
        {
            return existingTx; // Already processed
        }
        
        // 2. Validate Mode
        if (!await EnsureInventoryModeIsActiveAsync(tenantId, cancellationToken))
        {
            throw new DomainException("invalid_mode", "Inventory operating mode is not set to Internal or Hybrid.");
        }

        // 2b. Load settings once for negative-stock policy
        var settings = await _dbContext.TenantInventorySettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        // 3. Load or create StockBalance (Pessimistic-like concurrency can be handled by RowVersion later, or we lock)
        var stock = await _dbContext.StockBalances
            .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.CatalogItemId == catalogItemId, cancellationToken);

        if (stock == null)
        {
            if (quantity < 0)
            {
                throw new DomainException("insufficient_stock", "Cannot post negative quantity for non-existent stock balance.");
            }

            stock = new StockBalance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = warehouseId,
                CatalogItemId = catalogItemId,
                QuantityOnHand = 0,
                AverageUnitCost = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.StockBalances.Add(stock);
        }

        // 4. Calculate new stock and average cost
        var oldQuantity = stock.QuantityOnHand;
        var newQuantity = oldQuantity + quantity;

        var allowNegative = settings?.AllowNegativeStock ?? false;

        if (newQuantity < 0 && !allowNegative)
        {
            throw new DomainException("insufficient_stock", $"Insufficient stock. Current: {oldQuantity}, Requested: {Math.Abs(quantity)}", "Domain.InsufficientStock", oldQuantity, Math.Abs(quantity));
        }

        // Only recalculate average cost on positive stock entries (Receipts/Positive Adjustments)
        if (quantity > 0)
        {
            // Weighted Average Cost formula:
            // ((OldQty * OldAvgCost) + (AddedQty * UnitCost)) / NewQty
            var totalOldValue = oldQuantity * stock.AverageUnitCost;
            var addedValue = quantity * unitCost;
            stock.AverageUnitCost = (totalOldValue + addedValue) / newQuantity;
        }

        stock.QuantityOnHand = newQuantity;
        stock.UpdatedAt = DateTime.UtcNow;

        // 5. Create Transaction
        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            CatalogItemId = catalogItemId,
            MaintenanceOrderId = maintenanceOrderId,
            Type = type,
            State = InventoryTransactionState.Posted,
            Quantity = quantity,
            UnitCost = quantity > 0 ? unitCost : stock.AverageUnitCost, // For consumptions, use the moving average cost
            Reason = reason,
            CreatedBy = _currentUser.Id?.ToString() ?? Guid.Empty.ToString(),
            CreatedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey
        };

        _dbContext.InventoryTransactions.Add(transaction);

        return transaction;
    }
}
