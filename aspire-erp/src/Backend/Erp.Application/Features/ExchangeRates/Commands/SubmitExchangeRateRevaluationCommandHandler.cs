using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Application.Features.FiscalClosing;

namespace Erp.Application.Features.ExchangeRates.Commands;

public class SubmitExchangeRateRevaluationCommandHandler : ICommandHandler<SubmitExchangeRateRevaluationCommand, Result<SubmitExchangeRateRevaluationResult>>
{
    private readonly IExchangeRateRevaluationRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IFiscalYearRepository _fiscalYearRepository;
    private readonly IExchangeRateRepository _exchangeRateRepository;

    public SubmitExchangeRateRevaluationCommandHandler(
        IExchangeRateRevaluationRepository repository,
        ICompanyRepository companyRepository,
        IAccountRepository accountRepository,
        IFiscalYearRepository fiscalYearRepository,
        IExchangeRateRepository exchangeRateRepository)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _accountRepository = accountRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _exchangeRateRepository = exchangeRateRepository;
    }

    public async Task<Result<SubmitExchangeRateRevaluationResult>> HandleAsync(SubmitExchangeRateRevaluationCommand request, CancellationToken cancellationToken)
    {
        var revaluation = await _repository.GetByIdAsync(request.RevaluationId, cancellationToken);
        if (revaluation == null) throw new Exception("Revaluation not found");

        if (revaluation.DocumentStatus == DocumentStatus.Submitted)
        {
            return Result<SubmitExchangeRateRevaluationResult>.Success(new SubmitExchangeRateRevaluationResult(revaluation.Id, revaluation.VoucherNo));
        }

        FiscalClosingGuards.EnsureRowVersion(revaluation.RowVersion, request.RowVersion, "ExchangeRateRevaluation", revaluation.Id);
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        revaluation.EnsureCanSubmit(today);

        var company = await _companyRepository.GetByIdAsync(revaluation.CompanyId, cancellationToken);
        company!.EnsurePostingDateUnlocked(revaluation.PostingDate);

        revaluation.VoucherNo = "FX-REV-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        revaluation.DocumentStatus = DocumentStatus.Submitted;

        await _repository.UpdateAsync(revaluation, cancellationToken);

        return Result<SubmitExchangeRateRevaluationResult>.Success(new SubmitExchangeRateRevaluationResult(revaluation.Id, revaluation.VoucherNo));
    }
}
