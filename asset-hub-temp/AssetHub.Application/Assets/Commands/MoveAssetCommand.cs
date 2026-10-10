using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;

namespace AssetHub.Application.Assets.Commands;

public record MoveAssetCommand(Guid AssetId, Guid? NewParentId) : IRequest<bool>;

public class MoveAssetCommandHandler : IRequestHandler<MoveAssetCommand, bool>
{
    private readonly IAssetHierarchyService _hierarchyService;

    public MoveAssetCommandHandler(IAssetHierarchyService hierarchyService)
    {
        _hierarchyService = hierarchyService;
    }

    public async Task<bool> Handle(MoveAssetCommand request, CancellationToken cancellationToken)
    {
        await _hierarchyService.MoveAssetHierarchyAsync(request.AssetId, request.NewParentId, cancellationToken);
        return true;
    }
}
