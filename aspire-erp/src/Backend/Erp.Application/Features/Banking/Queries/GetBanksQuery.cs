using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>
/// Lists the offline master bank catalog (bank-catalog spec BNK-G1/G2/G5): optional ISO
/// country filter (null = "MX" read-level default, never persisted) plus optional
/// code/name search for autocomplete. Only active banks, ordered by code.
/// </summary>
public sealed record GetBanksQuery(
    string? CountryCode = null,
    string? Search = null) : IQuery<Result<IReadOnlyList<BankDto>>>;
