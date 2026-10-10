using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Infrastructure.Services;

public class AssetHierarchyService : IAssetHierarchyService
{
    private readonly ITenantDbContext _dbContext;

    public AssetHierarchyService(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InsertAssetHierarchyAsync(Guid assetId, Guid? parentId, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.Assets.FindAsync(new object[] { assetId }, cancellationToken);
        if (asset == null) return;

        // Path update
        if (parentId.HasValue)
        {
            var parent = await _dbContext.Assets.FindAsync(new object[] { parentId.Value }, cancellationToken);
            asset.Path = $"{parent?.Path}{assetId}/";
        }
        else
        {
            asset.Path = $"/{assetId}/";
        }

        // Self-reference in hierarchy
        _dbContext.AssetHierarchies.Add(new AssetHierarchy
        {
            AncestorId = assetId,
            DescendantId = assetId,
            Depth = 0
        });

        if (parentId.HasValue)
        {
            // Connect to ancestors
            var ancestors = await _dbContext.AssetHierarchies
                .Where(h => h.DescendantId == parentId.Value)
                .ToListAsync(cancellationToken);

            foreach (var ancestor in ancestors)
            {
                _dbContext.AssetHierarchies.Add(new AssetHierarchy
                {
                    AncestorId = ancestor.AncestorId,
                    DescendantId = assetId,
                    Depth = ancestor.Depth + 1
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAssetHierarchyAsync(Guid assetId, Guid? newParentId, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.Assets.FindAsync(new object[] { assetId }, cancellationToken);
        if (asset == null) return;

        if (assetId == newParentId)
            throw new InvalidOperationException("Asset cannot be its own parent");

        // 1. Prevent cycles
        if (newParentId.HasValue)
        {
            bool isDescendant = await _dbContext.AssetHierarchies
                .AnyAsync(h => h.AncestorId == assetId && h.DescendantId == newParentId.Value, cancellationToken);
            if (isDescendant)
                throw new InvalidOperationException("Cannot move asset to one of its descendants");
        }

        // Get all descendants in the subtree (including the asset itself)
        var subtreeNodes = await _dbContext.AssetHierarchies
            .Where(h => h.AncestorId == assetId)
            .Select(h => new { h.DescendantId, h.Depth })
            .ToListAsync(cancellationToken);

        var subtreeIds = subtreeNodes.Select(s => s.DescendantId).ToList();

        // 2. Disconnect A's subtree from A's old ancestors
        var oldRelations = await _dbContext.AssetHierarchies
            .Where(h => subtreeIds.Contains(h.DescendantId) && !subtreeIds.Contains(h.AncestorId))
            .ToListAsync(cancellationToken);

        _dbContext.AssetHierarchies.RemoveRange(oldRelations);

        // Update ParentId
        asset.ParentId = newParentId;

        // 3. Connect A's subtree to new ancestors and update path
        var oldBasePath = asset.Path;
        string newBasePath;

        if (newParentId.HasValue)
        {
            var newParent = await _dbContext.Assets.FindAsync(new object[] { newParentId.Value }, cancellationToken);
            if (newParent == null) throw new InvalidOperationException("New parent not found");

            var newAncestors = await _dbContext.AssetHierarchies
                .Where(h => h.DescendantId == newParentId.Value)
                .Select(h => new { h.AncestorId, h.Depth })
                .ToListAsync(cancellationToken);

            foreach (var newAncestor in newAncestors)
            {
                foreach (var subtreeNode in subtreeNodes)
                {
                    _dbContext.AssetHierarchies.Add(new AssetHierarchy
                    {
                        AncestorId = newAncestor.AncestorId,
                        DescendantId = subtreeNode.DescendantId,
                        Depth = newAncestor.Depth + subtreeNode.Depth + 1
                    });
                }
            }
            newBasePath = $"{newParent.Path}{assetId}/";
        }
        else
        {
            newBasePath = $"/{assetId}/";
        }

        var subtreeAssets = await _dbContext.Assets
            .Where(a => subtreeIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        foreach (var a in subtreeAssets)
        {
            if (a.Path.StartsWith(oldBasePath))
            {
                a.Path = newBasePath + a.Path.Substring(oldBasePath.Length);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteSubtreeAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var descendants = await _dbContext.AssetHierarchies
            .Where(h => h.AncestorId == assetId)
            .Select(h => h.DescendantId)
            .ToListAsync(cancellationToken);

        var assets = await _dbContext.Assets.Where(a => descendants.Contains(a.Id)).ToListAsync(cancellationToken);
        
        foreach (var a in assets)
        {
            a.IsDeleted = true;
            a.DeletedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
