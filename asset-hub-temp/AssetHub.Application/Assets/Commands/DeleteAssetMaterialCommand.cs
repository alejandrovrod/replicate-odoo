using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Commands;

public record DeleteAssetMaterialCommand(Guid MaterialId) : IRequest<Unit>;

public class DeleteAssetMaterialCommandHandler : IRequestHandler<DeleteAssetMaterialCommand, Unit>
{
    private readonly ITenantDbContext _dbContext;

    public DeleteAssetMaterialCommandHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(DeleteAssetMaterialCommand request, CancellationToken cancellationToken)
    {
        var material = await _dbContext.AssetMaterials
            .FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);

        if (material != null)
        {
            material.IsDeleted = true;
            material.DeletedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
