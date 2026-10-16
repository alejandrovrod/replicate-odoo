using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Suspends or releases a supplier hold (module buying-suppliers, spec §5
/// <c>POST /api/v1/suppliers/{id}/hold</c>). <see cref="OnHold"/> false releases; true applies
/// <see cref="HoldType"/> until <see cref="ReleaseDate"/> (null = no auto-release).
/// </summary>
public sealed record SetSupplierHoldStatusCommand(
    Guid Id,
    bool OnHold,
    SupplierHoldType HoldType,
    DateOnly? ReleaseDate,
    byte[]? RowVersion = null) : ICommand<Result<SupplierDto>>;
