using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Creates one SupplierGroup (module buying-suppliers, spec §1 gap D1). Duplicate codes are
/// rejected per TENANT and reported as a 409 domain failure through <see cref="Result{T}"/>.
/// </summary>
public sealed record CreateSupplierGroupCommand(
    string Code,
    string Name,
    bool IsActive = true) : ICommand<Result<SupplierGroupDto>>;
