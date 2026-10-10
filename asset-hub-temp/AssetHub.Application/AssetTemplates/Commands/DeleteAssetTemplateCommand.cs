using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.AssetTemplates.Commands;

public record DeleteAssetTemplateCommand(Guid Id) : IRequest<bool>;

public class DeleteAssetTemplateCommandHandler : IRequestHandler<DeleteAssetTemplateCommand, bool>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IAssetTemplateUsageChecker _usageChecker;

    public DeleteAssetTemplateCommandHandler(ITenantDbContext dbContext, IAssetTemplateUsageChecker usageChecker)
    {
        _dbContext = dbContext;
        _usageChecker = usageChecker;
    }

    public async Task<bool> Handle(DeleteAssetTemplateCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AssetTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.IsActive, cancellationToken);

        if (existing == null) return false;

        var usages = await _usageChecker.GetUsageCountAsync(existing.Id);
        if (usages > 0)
        {
            throw new AssetTemplateInUseException(usages);
        }

        existing.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
