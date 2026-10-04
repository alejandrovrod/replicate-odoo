using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists the heuristic rules of a company (active and inactive).</summary>
public sealed class GetBankTransactionRulesQueryHandler
    : IQueryHandler<GetBankTransactionRulesQuery, IReadOnlyList<BankTransactionRuleDto>>
{
    private readonly IBankRepository _bank;

    public GetBankTransactionRulesQueryHandler(IBankRepository bank)
    {
        _bank = bank;
    }

    public async Task<IReadOnlyList<BankTransactionRuleDto>> HandleAsync(
        GetBankTransactionRulesQuery query,
        CancellationToken cancellationToken = default)
    {
        var rules = await _bank.GetRulesByCompanyAsync(query.CompanyId, cancellationToken);

        return rules.Select(BankTransactionRuleDto.From).ToList();
    }
}
