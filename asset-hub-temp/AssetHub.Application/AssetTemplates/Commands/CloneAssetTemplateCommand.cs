using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.AssetTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.AssetTemplates.Commands;

public record CloneAssetTemplateCommand(Guid SourceTemplateId, string NewCode, string NewName) : IRequest<Guid>;

public class CloneAssetTemplateCommandHandler : IRequestHandler<CloneAssetTemplateCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public CloneAssetTemplateCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CloneAssetTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var sourceTemplate = await _dbContext.AssetTemplates
            .FirstOrDefaultAsync(t => t.Id == request.SourceTemplateId && t.TenantId == tenantId && t.IsActive, cancellationToken);
            
        if (sourceTemplate == null)
            throw new InvalidOperationException("Template de origen no encontrado.");

        var codeExists = await _dbContext.AssetTemplates
            .AnyAsync(t => t.Code == request.NewCode && t.TenantId == tenantId && t.IsActive, cancellationToken);
            
        if (codeExists)
            throw new InvalidOperationException($"Ya existe un template con el código {request.NewCode}");

        var newTemplate = new AssetTemplate
        {
            TenantId = tenantId,
            BusinessEntityTypeId = sourceTemplate.BusinessEntityTypeId,
            Code = request.NewCode,
            Name = request.NewName,
            Description = sourceTemplate.Description,
            SchemaJson = sourceTemplate.SchemaJson,
            AllowedChildTemplateIds = new System.Collections.Generic.List<Guid>(sourceTemplate.AllowedChildTemplateIds),
            LifecycleStates = sourceTemplate.LifecycleStates,
            MaintenanceChecklist = sourceTemplate.MaintenanceChecklist,
            Version = 1,
            IsActive = true
        };

        _dbContext.AssetTemplates.Add(newTemplate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newTemplate.Id;
    }
}
