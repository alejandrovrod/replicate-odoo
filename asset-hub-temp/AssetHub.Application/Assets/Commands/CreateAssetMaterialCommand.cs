using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Resources;
using AssetHub.Domain.Assets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AssetHub.Application.Assets.Commands;

public record CreateAssetMaterialCommand(
    Guid AssetId,
    Guid CatalogItemId,
    decimal Quantity,
    string UnitOfMeasure,
    bool IsCritical,
    string? Notes
) : IRequest<Guid>;

public class CreateAssetMaterialCommandValidator : AbstractValidator<CreateAssetMaterialCommand>
{
    public CreateAssetMaterialCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.AssetId).NotEmpty().WithMessage(localizer["Validation_NotEmpty"]);
        RuleFor(x => x.CatalogItemId).NotEmpty().WithMessage(localizer["Validation_NotEmpty"]);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(localizer["Validation_GreaterThan"]);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().WithMessage(localizer["Validation_NotEmpty"]);
    }
}

public class CreateAssetMaterialCommandHandler : IRequestHandler<CreateAssetMaterialCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public CreateAssetMaterialCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateAssetMaterialCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId() ?? throw new UnauthorizedAccessException("Tenant is required.");

        var exists = await _dbContext.AssetMaterials
            .AnyAsync(m => m.AssetId == request.AssetId && m.CatalogItemId == request.CatalogItemId, cancellationToken);
            
        if (exists)
        {
            // Message is a catalog key: ExceptionHandlingMiddleware resolves it for
            // the culture of the request instead of returning a hardcoded sentence.
            throw new InvalidOperationException("Domain.MaterialAlreadyAssociated"); // Handled as 409 Conflict in controller
        }

        var assetMaterial = new AssetMaterial
        {
            Id = Guid.NewGuid(), // Assuming v7 in a real scenario
            TenantId = tenantId,
            AssetId = request.AssetId,
            CatalogItemId = request.CatalogItemId,
            Quantity = request.Quantity,
            UnitOfMeasure = request.UnitOfMeasure,
            IsCritical = request.IsCritical,
            Notes = request.Notes
        };

        _dbContext.AssetMaterials.Add(assetMaterial);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return assetMaterial.Id;
    }
}
