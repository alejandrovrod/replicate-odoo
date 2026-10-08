using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Application.Features.FiscalClosing;

namespace Erp.Application.Features.ExchangeRates.Commands;

public class CancelExchangeRateRevaluationCommandHandler : ICommandHandler<CancelExchangeRateRevaluationCommand, Result<CancelExchangeRateRevaluationResult>>
{
    private readonly IExchangeRateRevaluationRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public CancelExchangeRateRevaluationCommandHandler(
        IExchangeRateRevaluationRepository repository,
        ICompanyRepository companyRepository,
        IFiscalYearRepository fiscalYearRepository)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<Result<CancelExchangeRateRevaluationResult>> HandleAsync(CancelExchangeRateRevaluationCommand request, CancellationToken cancellationToken)
    {
        var revaluation = await _repository.GetByIdAsync(request.RevaluationId, cancellationToken);
        if (revaluation == null)
        {
            throw new Exception("Revaluation not found");
        }

        if (revaluation.DocumentStatus == DocumentStatus.Cancelled)
        {
            return Result<CancelExchangeRateRevaluationResult>.Success(new CancelExchangeRateRevaluationResult(revaluation.Id));
        }

        FiscalClosingGuards.EnsureRowVersion(revaluation.RowVersion, request.RowVersion, "ExchangeRateRevaluation", revaluation.Id);

        revaluation.EnsureCanCancel();

        var company = await _companyRepository.GetByIdAsync(revaluation.CompanyId, cancellationToken);
        company!.EnsurePostingDateUnlocked(revaluation.PostingDate);

        revaluation.DocumentStatus = DocumentStatus.Cancelled;

        await _repository.UpdateAsync(revaluation, cancellationToken);

        return Result<CancelExchangeRateRevaluationResult>.Success(new CancelExchangeRateRevaluationResult(revaluation.Id));
    }
}
