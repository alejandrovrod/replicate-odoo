using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Capitalizes a Draft/Submitted asset (Task 10.2, scenario AS-01): clears its CWIP cost into
/// the Fixed Asset account (Dr Fixed / Cr CWIP for the gross amount), generates the straight-line
/// schedule (Task 10.3 engine) and moves the asset to Capitalized - all inside ONE transaction.
/// The asset master row itself is seeded beforehand (Draft with its purchase value); the gapless
/// AST-YYYY-NNNNN code is assigned here when still unset, inside the same transaction.
/// </summary>
public sealed record CapitalizeAssetCommand(
    Guid CompanyId,
    Guid AssetId,
    DateOnly CapitalizationDate) : ICommand<Result<AssetCapitalizationDto>>;
