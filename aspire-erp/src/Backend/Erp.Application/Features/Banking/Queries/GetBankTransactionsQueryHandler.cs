using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists the staging lines of a company through the repository (tenant filter automatic).</summary>
public sealed class GetBankTransactionsQueryHandler
    : IQueryHandler<GetBankTransactionsQuery, IReadOnlyList<BankTransactionDto>>
{
    private readonly IBankRepository _bank;

    public GetBankTransactionsQueryHandler(IBankRepository bank)
    {
        _bank = bank;
    }

    public async Task<IReadOnlyList<BankTransactionDto>> HandleAsync(
        GetBankTransactionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _bank.GetTransactionsAsync(
            query.CompanyId, query.BankAccountId, query.Status, cancellationToken);

        return transactions.Select(BankTransactionDto.From).ToList();
    }
}
