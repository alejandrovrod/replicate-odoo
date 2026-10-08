using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.ExchangeRates.Commands;

public class CreateExchangeRateRevaluationCommandHandler : ICommandHandler<CreateExchangeRateRevaluationCommand, Result<CreateExchangeRateRevaluationResult>>
{
    private readonly IExchangeRateRevaluationRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccountRepository _accountRepository;

    public CreateExchangeRateRevaluationCommandHandler(
        IExchangeRateRevaluationRepository repository,
        ICompanyRepository companyRepository,
        IAccountRepository accountRepository)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _accountRepository = accountRepository;
    }

    public async Task<Result<CreateExchangeRateRevaluationResult>> HandleAsync(CreateExchangeRateRevaluationCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company == null) throw new Exception("Company not found");

        company.EnsurePostingDateUnlocked(request.PostingDate);

        Guid accountId = request.ExchangeGainLossAccountId ?? Guid.Empty;
        if (accountId == Guid.Empty) throw new Exception("Exchange gain/loss account missing.");

        var revaluation = new ExchangeRateRevaluation
        {
            CompanyId = request.CompanyId,
            PostingDate = request.PostingDate,
            ExchangeGainLossAccountId = accountId,
            RoundingLossAllowance = request.RoundingLossAllowance,
            Remarks = request.Remarks,
            IdempotencyKey = request.IdempotencyKey,
            DocumentStatus = DocumentStatus.Draft
        };

        var balances = await _repository.GetForeignCurrencyBalancesAsync(request.CompanyId, request.PostingDate, cancellationToken);
        foreach (var bal in balances)
        {
            revaluation.Lines.Add(new ExchangeRateRevaluationLine
            {
                AccountId = bal.AccountId,
                CurrencyId = bal.CurrencyId,
                BalanceInForeignCurrency = bal.BalanceInForeignCurrency,
                BalanceInBaseCurrency = bal.BalanceInBaseCurrency,
                GainLossAmount = 0, // Computed on submit or dynamically
                CurrentExchangeRate = 1m, // Placeholder, normally queried
                NewExchangeRate = 1m
            });
        }

        await _repository.AddAsync(revaluation, cancellationToken);
        return Result<CreateExchangeRateRevaluationResult>.Success(new CreateExchangeRateRevaluationResult(revaluation.Id));
    }
}
