using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.ExchangeRates.Queries;

public class GetRevaluationPreviewQueryHandler : IQueryHandler<GetRevaluationPreviewQuery, Result<RevaluationPreviewDto>>
{
    private readonly IExchangeRateRevaluationRepository _repository;
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly ICompanyRepository _companyRepository;

    public GetRevaluationPreviewQueryHandler(
        IExchangeRateRevaluationRepository repository,
        IExchangeRateRepository exchangeRateRepository,
        ICompanyRepository companyRepository)
    {
        _repository = repository;
        _exchangeRateRepository = exchangeRateRepository;
        _companyRepository = companyRepository;
    }

    public async Task<Result<RevaluationPreviewDto>> HandleAsync(GetRevaluationPreviewQuery request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company == null || company.CurrencyId == null)
            throw new Exception("Company or base currency not found");

        var balances = await _repository.GetForeignCurrencyBalancesAsync(request.CompanyId, request.PostingDate, cancellationToken);
        
        var lines = new List<RevaluationPreviewLineDto>();
        decimal totalGainLoss = 0;
        
        foreach (var balance in balances)
        {
            var rate = await _exchangeRateRepository.GetLatestRateAsync(balance.CurrencyId, company.CurrencyId.Value, request.PostingDate, cancellationToken);
            if (rate == null) continue;
            
            var newBaseBalance = Math.Round(balance.BalanceInForeignCurrency * rate.Rate, 2);
            var gainLoss = newBaseBalance - balance.BalanceInBaseCurrency;
            totalGainLoss += gainLoss;
            
            lines.Add(new RevaluationPreviewLineDto(
                balance.AccountId,
                balance.CurrencyId,
                balance.BalanceInForeignCurrency,
                balance.BalanceInBaseCurrency,
                rate.Rate,
                gainLoss
            ));
        }

        return Result<RevaluationPreviewDto>.Success(new RevaluationPreviewDto(
            request.CompanyId,
            request.PostingDate,
            lines,
            totalGainLoss
        ));
    }
}
