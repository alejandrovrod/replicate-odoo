using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Currencies.Commands;

/// <summary>
/// Creates one ISO currency in the global catalog (RM-09). Duplicate codes are rejected
/// globally and reported as a 409 domain failure through <see cref="Result{T}"/> - the same
/// pattern as CreateSupplierCommand.
/// </summary>
public sealed record CreateCurrencyCommand(
    string Code,
    string Symbol,
    string FractionName = "",
    bool IsActive = true) : ICommand<Result<CurrencyDto>>;
