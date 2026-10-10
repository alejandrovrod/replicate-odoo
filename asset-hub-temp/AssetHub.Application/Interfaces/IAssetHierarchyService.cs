using System;
using System.Threading;
using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface IAssetHierarchyService
{
    Task InsertAssetHierarchyAsync(Guid assetId, Guid? parentId, CancellationToken cancellationToken);
    Task MoveAssetHierarchyAsync(Guid assetId, Guid? newParentId, CancellationToken cancellationToken);
    Task SoftDeleteSubtreeAsync(Guid assetId, CancellationToken cancellationToken);
}
