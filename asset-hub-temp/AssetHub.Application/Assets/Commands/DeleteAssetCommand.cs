using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Commands;

public record DeleteAssetCommand(Guid Id) : IRequest;

public class DeleteAssetCommandHandler : IRequestHandler<DeleteAssetCommand>
{
    private readonly ITenantDbContext _dbContext;

    public DeleteAssetCommandHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (asset == null)
            throw new InvalidOperationException("Activo no encontrado.");

        var hierarchies = await _dbContext.AssetHierarchies
            .Where(h => h.AncestorId == request.Id || h.DescendantId == request.Id)
            .ToListAsync(cancellationToken);
        _dbContext.AssetHierarchies.RemoveRange(hierarchies);

        var attachments = await _dbContext.AssetAttachments
            .Where(a => a.AssetId == request.Id)
            .ToListAsync(cancellationToken);
        _dbContext.AssetAttachments.RemoveRange(attachments);

        var conditions = await _dbContext.AssetConditionHistories
            .Where(c => c.AssetId == request.Id)
            .ToListAsync(cancellationToken);
        _dbContext.AssetConditionHistories.RemoveRange(conditions);

        var events = await _dbContext.AssetLifecycleEvents
            .Where(e => e.AssetId == request.Id)
            .ToListAsync(cancellationToken);
        _dbContext.AssetLifecycleEvents.RemoveRange(events);

        var attributes = await _dbContext.AssetAttributeValues
            .Where(a => a.AssetId == request.Id)
            .ToListAsync(cancellationToken);
        _dbContext.AssetAttributeValues.RemoveRange(attributes);

        _dbContext.Assets.Remove(asset);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
