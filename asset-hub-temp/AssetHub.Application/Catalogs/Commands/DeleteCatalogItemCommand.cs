using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Catalogs.Commands;

public record DeleteCatalogItemCommand(string CatalogCode, string ItemCode) : IRequest<bool>;

public class DeleteCatalogItemCommandHandler : IRequestHandler<DeleteCatalogItemCommand, bool>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ICatalogUsageChecker _usageChecker;

    public DeleteCatalogItemCommandHandler(ITenantDbContext dbContext, ICatalogUsageChecker usageChecker)
    {
        _dbContext = dbContext;
        _usageChecker = usageChecker;
    }

    public async Task<bool> Handle(DeleteCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.CatalogItems
            .Include(ci => ci.Catalog)
            .FirstOrDefaultAsync(ci => ci.Catalog!.Code == request.CatalogCode && ci.Code == request.ItemCode, cancellationToken);

        if (item == null) return false;

        var usages = await _usageChecker.GetUsageCountAsync(item.Id);
        if (usages > 0)
        {
            throw new CatalogInUseException($"El ítem está siendo usado por {usages} entidades.");
        }

        item.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
