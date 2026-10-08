using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.FiscalClosing;

/// <summary>
/// Creates one <c>Draft</c> closing voucher bound to a fiscal year (spec FC-04/FC-05).
/// The voucher posts NOTHING yet — GL impact happens exactly once, on submit.
/// </summary>
public sealed record CreatePeriodClosingVoucherCommand(
    Guid CompanyId,
    Guid FiscalYearId,
    DateOnly PostingDate,
    Guid RetainedEarningsAccountId,
    string? Remarks,
    string? IdempotencyKey) : ICommand<Result<PeriodClosingVoucherDto>>;

/// <summary>Paged voucher list with fiscal-year + status filters.</summary>
public sealed record GetPeriodClosingVouchersQuery(
    Guid CompanyId,
    Guid? FiscalYearId = null,
    DocumentStatus? Status = null,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<PeriodClosingVoucherDto>>;

/// <summary>Single voucher detail (header + derived lines).</summary>
public sealed record GetPeriodClosingVoucherDetailQuery(
    Guid VoucherId,
    Guid CompanyId) : IQuery<PeriodClosingVoucherDto?>;

/// <summary>
/// Read-only pre-submit P&amp;L preview: the EXACT line set submit would post, from the SAME
/// balance query (plan.md §4). No writes.
/// </summary>
public sealed record GetUnclosedPLBalancesQuery(
    Guid CompanyId,
    Guid FiscalYearId) : IQuery<ClosingPreviewDto?>;

public sealed class CreatePeriodClosingVoucherCommandHandler
    : ICommandHandler<CreatePeriodClosingVoucherCommand, Result<PeriodClosingVoucherDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IFiscalYearRepository _years;
    private readonly IAccountRepository _accounts;
    private readonly IPeriodClosingVoucherRepository _closings;

    public CreatePeriodClosingVoucherCommandHandler(
        ICompanyRepository companies,
        IFiscalYearRepository years,
        IAccountRepository accounts,
        IPeriodClosingVoucherRepository closings)
    {
        _companies = companies;
        _years = years;
        _accounts = accounts;
        _closings = closings;
    }

    public async Task<Result<PeriodClosingVoucherDto>> HandleAsync(
        CreatePeriodClosingVoucherCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _closings.ExecuteInSerializableTransactionAsync(async () =>
            {
                // Idempotent create: same key replays the same Draft (spec FC-05).
                if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
                {
                    var replay = await _closings.GetByIdempotencyKeyAsync(
                        command.CompanyId, command.IdempotencyKey!, cancellationToken);
                    if (replay is not null)
                    {
                        return Result<PeriodClosingVoucherDto>.Success(PeriodClosingVoucherDto.Build(replay));
                    }
                }

                var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.PeriodClosingNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                var year = await _years.GetByIdAsync(command.FiscalYearId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Fiscal year '{command.FiscalYearId}' was not found in this tenant.");

                if (year.CompanyId != command.CompanyId)
                {
                    throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Fiscal year '{year.YearName}' does not belong to company '{command.CompanyId}'.");
                }

                if (year.IsClosed)
                {
                    throw new FiscalYearClosedException(year.Id, year.YearName);
                }

                if (command.PostingDate < year.StartDate || command.PostingDate > year.EndDate)
                {
                    throw new ClosingDateOutsideFiscalYearException(
                        command.PostingDate, year.YearName, year.StartDate, year.EndDate);
                }

                // Plan.md §3 hard lock: frozen check first, then the closed-year guard.
                company.EnsurePostingDateUnlocked(command.PostingDate);
                await _years.EnsurePostingDateInOpenYearAsync(command.CompanyId, command.PostingDate, cancellationToken);

                var retained = await FiscalClosingGuards.ResolveRetainedEarningsAsync(
                    _accounts, company, command.RetainedEarningsAccountId, cancellationToken);

                var voucher = new PeriodClosingVoucher
                {
                    Id = Guid.NewGuid(),
                    CompanyId = command.CompanyId,
                    FiscalYearId = year.Id,
                    VoucherNo = $"DRAFT-{year.StartDate.Year}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                    PostingDate = command.PostingDate,
                    RetainedEarningsAccountId = retained.Id,
                    DocumentStatus = DocumentStatus.Draft,
                    Remarks = command.Remarks,
                    IdempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey) ? null : command.IdempotencyKey,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                await _closings.AddAsync(voucher, cancellationToken);
                return Result<PeriodClosingVoucherDto>.Success(PeriodClosingVoucherDto.Build(voucher));
            }, cancellationToken);
        }
        catch (FiscalClosingException ex)
        {
            return Result<PeriodClosingVoucherDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<PeriodClosingVoucherDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class GetPeriodClosingVouchersQueryHandler
    : IQueryHandler<GetPeriodClosingVouchersQuery, PagedResult<PeriodClosingVoucherDto>>
{
    private readonly IPeriodClosingVoucherRepository _closings;

    public GetPeriodClosingVouchersQueryHandler(IPeriodClosingVoucherRepository closings)
    {
        _closings = closings;
    }

    public async Task<PagedResult<PeriodClosingVoucherDto>> HandleAsync(
        GetPeriodClosingVouchersQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, (query.Page - 1) * query.PageSize);
        var items = await _closings.GetPagedAsync(
            query.CompanyId, query.FiscalYearId, query.Status, skip, query.PageSize, cancellationToken);
        return new PagedResult<PeriodClosingVoucherDto>(
            items.Select(PeriodClosingVoucherDto.Build).ToList(), items.Count, query.Page, query.PageSize);
    }
}

public sealed class GetPeriodClosingVoucherDetailQueryHandler
    : IQueryHandler<GetPeriodClosingVoucherDetailQuery, PeriodClosingVoucherDto?>
{
    private readonly IPeriodClosingVoucherRepository _closings;

    public GetPeriodClosingVoucherDetailQueryHandler(IPeriodClosingVoucherRepository closings)
    {
        _closings = closings;
    }

    public async Task<PeriodClosingVoucherDto?> HandleAsync(
        GetPeriodClosingVoucherDetailQuery query, CancellationToken cancellationToken = default)
    {
        var voucher = await _closings.GetByIdAsync(query.VoucherId, cancellationToken);
        return voucher is null || voucher.CompanyId != query.CompanyId
            ? null
            : PeriodClosingVoucherDto.Build(voucher);
    }
}

public sealed class GetUnclosedPLBalancesQueryHandler
    : IQueryHandler<GetUnclosedPLBalancesQuery, ClosingPreviewDto?>
{
    private readonly IFiscalYearRepository _years;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IPeriodClosingVoucherRepository _closings;

    public GetUnclosedPLBalancesQueryHandler(
        IFiscalYearRepository years,
        ICompanyRepository companies,
        IAccountRepository accounts,
        IPeriodClosingVoucherRepository closings)
    {
        _years = years;
        _companies = companies;
        _accounts = accounts;
        _closings = closings;
    }

    public async Task<ClosingPreviewDto?> HandleAsync(
        GetUnclosedPLBalancesQuery query, CancellationToken cancellationToken = default)
    {
        var year = await _years.GetByIdAsync(query.FiscalYearId, cancellationToken);
        if (year is null || year.CompanyId != query.CompanyId)
        {
            return null;
        }

        var balances = await _closings.GetUnclosedPLBalancesAsync(query.CompanyId, query.FiscalYearId, cancellationToken);
        if (balances.Count == 0)
        {
            return new ClosingPreviewDto(query.CompanyId, query.FiscalYearId, Array.Empty<ClosingPreviewLineDto>(), null, 0m);
        }

        var company = await _companies.GetByIdAsync(query.CompanyId, cancellationToken);
        var retainedId = company?.DefaultRetainedEarningsAccountId ?? Guid.Empty;
        string retainedCode = retainedId.ToString();
        string retainedName = string.Empty;
        if (retainedId != Guid.Empty)
        {
            var retained = await _accounts.GetByIdAsync(retainedId, cancellationToken);
            if (retained is not null)
            {
                retainedCode = retained.AccountCode;
                retainedName = retained.AccountName;
            }
        }

        return FiscalClosingGuards.BuildPreview(
            query.CompanyId, query.FiscalYearId, balances, retainedId, retainedCode, retainedName);
    }
}
