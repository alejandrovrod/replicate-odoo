using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Currencies.Commands;

/// <summary>
/// Executes <see cref="UpdateCurrencyCommand"/>: existence check, fail-fast RowVersion
/// compare-and-swap, field validation, duplicate-code rule (excluding self) and persistence.
/// </summary>
public sealed class UpdateCurrencyCommandHandler : ICommandHandler<UpdateCurrencyCommand, Result<CurrencyDto>>
{
    private readonly ICurrencyRepository _currencies;

    public UpdateCurrencyCommandHandler(ICurrencyRepository currencies)
    {
        _currencies = currencies;
    }

    public async Task<Result<CurrencyDto>> HandleAsync(
        UpdateCurrencyCommand request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currency = await _currencies.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new CurrencyValidationException(
                    CurrencyErrorCodes.CurrencyNotFound,
                    $"Currency '{request.Id}' was not found.");

            if (currency.RowVersion is null || !currency.RowVersion.AsSpan().SequenceEqual(request.RowVersion))
            {
                throw new ConcurrencyConflictException(nameof(Currency), currency.Id);
            }

            CurrencyValidator.EnsureValidFields(request.Code, request.Symbol, request.FractionName);

            var code = request.Code.Trim().ToUpperInvariant();
            if (!string.Equals(currency.Code, code, StringComparison.OrdinalIgnoreCase)
                && await _currencies.ExistsCodeAsync(code, cancellationToken))
            {
                throw new CurrencyValidationException(
                    CurrencyErrorCodes.DuplicateCurrencyCode,
                    $"Currency code '{code}' already exists.");
            }

            currency.Code = code;
            currency.Symbol = request.Symbol.Trim();
            currency.FractionName = request.FractionName?.Trim() ?? string.Empty;
            currency.IsActive = request.IsActive;

            await _currencies.UpdateAsync(currency, cancellationToken);

            return Result<CurrencyDto>.Success(CurrencyDto.From(currency));
        }
        catch (CurrencyValidationException ex)
        {
            return Result<CurrencyDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<CurrencyDto>.Failure(ex.Code, ex.Message);
        }
    }
}
