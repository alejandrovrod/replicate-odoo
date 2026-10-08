using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.ExchangeRates.Queries;

public class GetExchangeRateRevaluationDetailQueryHandler : IQueryHandler<GetExchangeRateRevaluationDetailQuery, Result<ExchangeRateRevaluationDetailDto>>
{
    private readonly IExchangeRateRevaluationRepository _repository;
    private readonly IAccountRepository _accounts;

    public GetExchangeRateRevaluationDetailQueryHandler(IExchangeRateRevaluationRepository repository, IAccountRepository accounts)
    {
        _repository = repository;
        _accounts = accounts;
    }

    public async Task<Result<ExchangeRateRevaluationDetailDto>> HandleAsync(GetExchangeRateRevaluationDetailQuery request, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (item == null)
            return Result<ExchangeRateRevaluationDetailDto>.Failure("revaluation_not_found", $"Revaluation {request.Id} not found.");

        var companyAccounts = await _accounts.GetByCompanyAsync(item.CompanyId, cancellationToken);
        var accounts = companyAccounts.ToDictionary(a => a.Id);

        var dto = new ExchangeRateRevaluationDto(
            item.Id,
            item.CompanyId,
            item.VoucherNo,
            item.PostingDate,
            item.ExchangeGainLossAccountId,
            item.RoundingLossAllowance,
            item.DocumentStatus.ToString(),
            item.Remarks,
            item.RowVersion
        );

        var lines = item.Lines.Select(l => new ExchangeRateRevaluationLineDto(
            l.AccountId,
            accounts.TryGetValue(l.AccountId, out var acc) ? acc.AccountCode : string.Empty,
            l.BalanceInForeignCurrency,
            l.BalanceInBaseCurrency,
            l.CurrentExchangeRate,
            l.NewExchangeRate,
            l.BalanceInBaseCurrency + l.GainLossAmount,
            l.GainLossAmount,
            l.BalanceInForeignCurrency == 0
        )).ToList();

        return Result<ExchangeRateRevaluationDetailDto>.Success(new ExchangeRateRevaluationDetailDto(dto, lines));
    }
}
