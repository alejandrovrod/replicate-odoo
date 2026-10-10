using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Resources;
using AssetHub.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AssetHub.Application.Assets.Commands;

public record UpdateAssetMaterialCommand(
    Guid MaterialId,
    decimal Quantity,
    string UnitOfMeasure,
    bool IsCritical,
    string? Notes
) : IRequest<Unit>;

public class UpdateAssetMaterialCommandValidator : AbstractValidator<UpdateAssetMaterialCommand>
{
    public UpdateAssetMaterialCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.MaterialId).NotEmpty().WithMessage(localizer["Validation_NotEmpty"]);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(localizer["Validation_GreaterThan"]);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().WithMessage(localizer["Validation_NotEmpty"]);
    }
}

public class UpdateAssetMaterialCommandHandler : IRequestHandler<UpdateAssetMaterialCommand, Unit>
{
    private readonly ITenantDbContext _dbContext;

    public UpdateAssetMaterialCommandHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(UpdateAssetMaterialCommand request, CancellationToken cancellationToken)
    {
        var material = await _dbContext.AssetMaterials
            .FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);

        if (material == null)
        {
            throw new NotFoundException("asset_material", request.MaterialId); // Return 404
        }

        material.Quantity = request.Quantity;
        material.UnitOfMeasure = request.UnitOfMeasure;
        material.IsCritical = request.IsCritical;
        material.Notes = request.Notes;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
