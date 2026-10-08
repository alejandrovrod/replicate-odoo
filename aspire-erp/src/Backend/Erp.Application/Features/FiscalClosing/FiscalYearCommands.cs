using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.FiscalClosing;

/// <summary>Creates one OPEN fiscal year (spec FC-04): no overlap with the same company's years.</summary>
public sealed record CreateFiscalYearCommand(
    Guid CompanyId,
    string YearName,
    DateOnly StartDate,
    DateOnly EndDate) : ICommand<Result<FiscalYearDto>>;

/// <summary>Hard-locks the year: <c>IsClosed = 1</c> (spec FC-04). Requires the current RowVersion.</summary>
public sealed record CloseFiscalYearCommand(
    Guid FiscalYearId,
    Guid CompanyId,
    byte[]? RowVersion) : ICommand<Result<FiscalYearDto>>;

/// <summary>Paged fiscal-year list with an optional <c>IsClosed</c> filter.</summary>
public sealed record GetFiscalYearsQuery(
    Guid CompanyId,
    bool? IsClosed = null,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<FiscalYearDto>>;

public sealed class CreateFiscalYearCommandHandler : ICommandHandler<CreateFiscalYearCommand, Result<FiscalYearDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IFiscalYearRepository _years;

    public CreateFiscalYearCommandHandler(ICompanyRepository companies, IFiscalYearRepository years)
    {
        _companies = companies;
        _years = years;
    }

    public async Task<Result<FiscalYearDto>> HandleAsync(CreateFiscalYearCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _years.ExecuteInSerializableTransactionAsync(async () =>
            {
                var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                var year = new FiscalYear
                {
                    Id = Guid.NewGuid(),
                    CompanyId = command.CompanyId,
                    YearName = command.YearName,
                    StartDate = command.StartDate,
                    EndDate = command.EndDate,
                    IsClosed = false,
                    ClosedAt = null,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                try
                {
                    year.EnsureValidBoundary();
                }
                catch (ArgumentException ex)
                {
                    throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.ClosingDateOutsideFiscalYear, ex.Message);
                }

                if (await _years.HasOverlapAsync(company.Id, year.StartDate, year.EndDate, null, cancellationToken))
                {
                    throw new FiscalYearOverlapException(year.YearName, year.StartDate, year.EndDate);
                }

                await _years.AddAsync(year, cancellationToken);
                return Result<FiscalYearDto>.Success(FiscalYearDto.Build(year));
            }, cancellationToken);
        }
        catch (FiscalClosingException ex)
        {
            return Result<FiscalYearDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class CloseFiscalYearCommandHandler : ICommandHandler<CloseFiscalYearCommand, Result<FiscalYearDto>>
{
    private readonly IFiscalYearRepository _years;
    private readonly IPeriodClosingVoucherRepository _closings;

    public CloseFiscalYearCommandHandler(IFiscalYearRepository years, IPeriodClosingVoucherRepository closings)
    {
        _years = years;
        _closings = closings;
    }

    public async Task<Result<FiscalYearDto>> HandleAsync(CloseFiscalYearCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _years.ExecuteInSerializableTransactionAsync(async () =>
            {
                var year = await _years.GetByIdAsync(command.FiscalYearId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Fiscal year '{command.FiscalYearId}' was not found in this tenant.");

                if (year.CompanyId != command.CompanyId)
                {
                    throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Fiscal year '{command.FiscalYearId}' does not belong to company '{command.CompanyId}'.");
                }

                FiscalClosingGuards.EnsureRowVersion(year.RowVersion, command.RowVersion, nameof(FiscalYear), year.Id);

                if (await _closings.HasDraftVoucherAsync(year.Id, cancellationToken))
                {
                    throw new FiscalClosingTransitionException(
                        FiscalClosingErrorCodes.PeriodClosingInvalidTransition,
                        $"Fiscal year '{year.YearName}' cannot be closed while a Draft closing voucher "
                        + "is still pending: submit or delete it first (spec FC-14).");
                }

                year.Close();
                _years.Update(year);
                return Result<FiscalYearDto>.Success(FiscalYearDto.Build(year));
            }, cancellationToken);
        }
        catch (FiscalClosingException ex)
        {
            return Result<FiscalYearDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<FiscalYearDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class GetFiscalYearsQueryHandler : IQueryHandler<GetFiscalYearsQuery, PagedResult<FiscalYearDto>>
{
    private readonly IFiscalYearRepository _years;

    public GetFiscalYearsQueryHandler(IFiscalYearRepository years)
    {
        _years = years;
    }

    public async Task<PagedResult<FiscalYearDto>> HandleAsync(GetFiscalYearsQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, (query.Page - 1) * query.PageSize);
        var items = await _years.GetPagedAsync(query.CompanyId, query.IsClosed, skip, query.PageSize, cancellationToken);
        return new PagedResult<FiscalYearDto>(items.Select(FiscalYearDto.Build).ToList(), items.Count, query.Page, query.PageSize);
    }
}
