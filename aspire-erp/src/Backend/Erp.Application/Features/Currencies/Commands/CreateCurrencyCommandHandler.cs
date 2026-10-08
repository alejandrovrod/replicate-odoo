using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Currencies.Commands;

/// <summary>
/// Executes <see cref="CreateCurrencyCommand"/>: pure Domain validation (CurrencyValidator),
/// the duplicate-code rule and persistence.
/// </summary>
public sealed class CreateCurrencyCommandHandler : ICommandHandler<CreateCurrencyCommand, Result<CurrencyDto>>
{
    private readonly ICurrencyRepository _currencies;

    public CreateCurrencyCommandHandler(ICurrencyRepository currencies)
    {
        _currencies = currencies;
    }

    public async Task<Result<CurrencyDto>> HandleAsync(
        CreateCurrencyCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CurrencyValidator.EnsureValidFields(command.Code, command.Symbol, command.FractionName);

            var code = command.Code.Trim().ToUpperInvariant();

            if (await _currencies.ExistsCodeAsync(code, cancellationToken))
            {
                throw new CurrencyValidationException(
                    CurrencyErrorCodes.DuplicateCurrencyCode,
                    $"Currency code '{code}' already exists.");
            }

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Code = code,
                Symbol = command.Symbol.Trim(),
                FractionName = command.FractionName?.Trim() ?? string.Empty,
                IsActive = command.IsActive,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _currencies.AddAsync(currency, cancellationToken);

            return Result<CurrencyDto>.Success(CurrencyDto.From(currency));
        }
        catch (CurrencyValidationException ex)
        {
            return Result<CurrencyDto>.Failure(ex.Code, ex.Message);
        }
    }
}
