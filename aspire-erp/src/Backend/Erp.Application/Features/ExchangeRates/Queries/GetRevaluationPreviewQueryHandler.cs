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
    private readonly ICurrencyRepository _currencyRepository;

    public GetRevaluationPreviewQueryHandler(
        IExchangeRateRevaluationRepository repository,
        IExchangeRateRepository exchangeRateRepository,
        ICompanyRepository companyRepository,
        ICurrencyRepository currencyRepository)
    {
        _repository = repository;
        _exchangeRateRepository = exchangeRateRepository;
        _companyRepository = companyRepository;
        _currencyRepository = currencyRepository;
    }

    public async Task<Result<RevaluationPreviewDto>> HandleAsync(GetRevaluationPreviewQuery request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company == null)
            return Result<RevaluationPreviewDto>.Failure(new Error("company_not_found", "Company not found"));

        Guid baseCurrencyId;
        if (company.CurrencyId.HasValue)
        {
            baseCurrencyId = company.CurrencyId.Value;
        }
        else
        {
            var usd = await _currencyRepository.GetByCodeAsync("USD", cancellationToken);
            if (usd == null)
                return Result<RevaluationPreviewDto>.Failure(new Error("base_currency_not_found", "Base currency not found"));
            baseCurrencyId = usd.Id;
        }

        var balances = await _repository.GetForeignCurrencyBalancesAsync(request.CompanyId, request.PostingDate, cancellationToken);
        
        var lines = new List<RevaluationPreviewLineDto>();
        decimal totalGainLoss = 0;
        
        foreach (var balance in balances)
        {
            var rate = await _exchangeRateRepository.GetLatestRateAsync(balance.CurrencyId, baseCurrencyId, request.PostingDate, cancellationToken);
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
