using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.EntityTypes.Commands;

public record DeleteEntityTypeCommand(string Code) : IRequest<bool>;

public class DeleteEntityTypeCommandHandler : IRequestHandler<DeleteEntityTypeCommand, bool>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IEntityTypeUsageChecker _usageChecker;

    public DeleteEntityTypeCommandHandler(ITenantDbContext dbContext, IEntityTypeUsageChecker usageChecker)
    {
        _dbContext = dbContext;
        _usageChecker = usageChecker;
    }

    public async Task<bool> Handle(DeleteEntityTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.BusinessEntityTypes
            .FirstOrDefaultAsync(e => e.Code == request.Code && e.IsActive, cancellationToken);

        if (entity == null) return false;

        var usages = await _usageChecker.GetUsageCountAsync(entity.Id);
        if (usages > 0)
        {
            throw new BusinessEntityTypeInUseException(usages);
        }

        entity.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
