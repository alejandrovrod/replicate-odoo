using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists the heuristic rules of a company (active and inactive).</summary>
public sealed class GetBankTransactionRulesQueryHandler
    : IQueryHandler<GetBankTransactionRulesQuery, PagedResult<BankTransactionRuleDto>>
{
    private readonly IBankRepository _bank;

    public GetBankTransactionRulesQueryHandler(IBankRepository bank)
    {
        _bank = bank;
    }

    public async Task<PagedResult<BankTransactionRuleDto>> HandleAsync(
        GetBankTransactionRulesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _bank.GetRulesByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return page.Map(page.Items.Select(BankTransactionRuleDto.From).ToList());
    }
}
