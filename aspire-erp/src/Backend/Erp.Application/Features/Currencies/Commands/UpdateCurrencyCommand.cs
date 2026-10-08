using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Currencies.Commands;

/// <summary>
/// Updates one ISO currency (RM-09). Carries the original <c>RowVersion</c> for optimistic
/// concurrency: a stale token fails with <c>concurrency_conflict</c> (409) instead of winning.
/// </summary>
public sealed record UpdateCurrencyCommand(
    Guid Id,
    string Code,
    string Symbol,
    string? FractionName,
    bool IsActive,
    byte[] RowVersion) : ICommand<Result<CurrencyDto>>;
