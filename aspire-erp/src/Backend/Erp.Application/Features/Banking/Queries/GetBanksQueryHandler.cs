using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>
/// Lists the offline catalog banks through the repository (tenant filter automatic).
/// The country is normalized to uppercase ISO 3166-1 alpha-2; an unknown or malformed
/// country fails with <c>invalid_bank_country</c> (spec BNK-G5, mapped to 400 by the API).
/// </summary>
public sealed class GetBanksQueryHandler
    : IQueryHandler<GetBanksQuery, Result<IReadOnlyList<BankDto>>>
{
    private readonly IBankRepository _banks;

    public GetBanksQueryHandler(IBankRepository banks)
    {
        _banks = banks;
    }

    public async Task<Result<IReadOnlyList<BankDto>>> HandleAsync(
        GetBanksQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Read-level default "MX" (spec §3): applied here, never persisted anywhere.
            var country = string.IsNullOrWhiteSpace(query.CountryCode)
                ? "MX"
                : BankValidator.NormalizeCountryCode(query.CountryCode);

            var banks = await _banks.GetBanksByCountryAsync(country, query.Search, cancellationToken);
            return Result<IReadOnlyList<BankDto>>.Success(banks.Select(BankDto.From).ToList());
        }
        catch (BankingValidationException ex)
        {
            return Result<IReadOnlyList<BankDto>>.Failure(ex.Code, ex.Message);
        }
    }
}
