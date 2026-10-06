using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists the staging lines of a company through the repository (tenant filter automatic).</summary>
public sealed class GetBankTransactionsQueryHandler
    : IQueryHandler<GetBankTransactionsQuery, PagedResult<BankTransactionDto>>
{
    private readonly IBankRepository _bank;

    public GetBankTransactionsQueryHandler(IBankRepository bank)
    {
        _bank = bank;
    }

    public async Task<PagedResult<BankTransactionDto>> HandleAsync(
        GetBankTransactionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _bank.GetTransactionsAsync(
            query.CompanyId,
            query.BankAccountId,
            query.Status,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return page.Map(page.Items.Select(BankTransactionDto.From).ToList());
    }
}
