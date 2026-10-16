using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Tax.Commands;

/// <summary>One template line on the wire (tax-engine Fase 2 catalog).</summary>
public sealed record TaxTemplateLineInput(
    Guid AccountId,
    string Description,
    decimal Rate,
    TaxChargeType ChargeType = TaxChargeType.Add,
    TaxCalculationBasis CalculationBasis = TaxCalculationBasis.OnNetTotal,
    decimal Amount = 0m);

/// <summary>Registers one tax template for a company (tax-engine Fase 2 catalog).</summary>
public sealed record CreateTaxTemplateCommand(
    Guid CompanyId,
    string Title,
    string Category,
    bool IsDefault = false,
    IReadOnlyList<TaxTemplateLineInput>? Lines = null) : ICommand<Result<TaxTemplateDto>>;

/// <summary>Replaces one template (title/category/default + wholesale line replacement).</summary>
public sealed record UpdateTaxTemplateCommand(
    Guid Id,
    Guid CompanyId,
    string Title,
    string Category,
    bool IsDefault,
    IReadOnlyList<TaxTemplateLineInput>? Lines = null) : ICommand<Result<TaxTemplateDto>>;

/// <summary>Hard-deletes one catalog template with its lines (config, never ledger data).</summary>
public sealed record DeleteTaxTemplateCommand(Guid Id, Guid CompanyId) : ICommand<Result<bool>>;

internal static class TaxTemplateGuard
{
    internal static readonly string[] Categories = ["Sales", "Purchase"];

    internal static string NormalizeCategory(string category)
    {
        var normalized = (category ?? string.Empty).Trim();
        var match = Categories.FirstOrDefault(
            c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxTemplateCategory,
                $"Tax template category must be 'Sales' or 'Purchase' (received '{category}').");
        }

        return match;
    }

    internal static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 150)
        {
            throw new TaxValidationException(
                TaxErrorCodes.DuplicateTaxTemplate,
                "Tax template title is required and must be at most 150 characters.");
        }

        return title.Trim();
    }

    /// <summary>
    /// Line rule (TAX-G4): every line REQUIRES a GL account
    /// (<c>tax_account_required</c>); rates are 0-100 unless the basis is FixedAmount
    /// (then the fixed amount must be non-negative); charge/basis enums must be known.
    /// Account existence/postability is resolved by the caller (handlers own data access).
    /// </summary>
    internal static void EnsureValidLine(TaxTemplateLineInput line, int index)
    {
        if (line.AccountId == Guid.Empty)
        {
            throw new TaxValidationException(
                TaxErrorCodes.TaxAccountRequired,
                $"Template line {index + 1} must reference a GL account (AccountId is required).");
        }

        if (line.ChargeType is not TaxChargeType.Add and not TaxChargeType.Deduct)
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxChargeType,
                $"Template line {index + 1} has an unknown charge type '{(int)line.ChargeType}'.");
        }

        if (line.CalculationBasis is not TaxCalculationBasis.OnNetTotal
            and not TaxCalculationBasis.OnPreviousRow
            and not TaxCalculationBasis.FixedAmount)
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxCalculationBasis,
                $"Template line {index + 1} has an unknown calculation basis '{(int)line.CalculationBasis}'.");
        }

        if (line.CalculationBasis == TaxCalculationBasis.FixedAmount)
        {
            if (line.Amount < 0)
            {
                throw new TaxValidationException(
                    TaxErrorCodes.InvalidTaxRate,
                    $"Template line {index + 1} fixed amount must not be negative.");
            }
        }
        else if (line.Rate < 0 || line.Rate > 100)
        {
            throw new TaxValidationException(
                TaxErrorCodes.InvalidTaxRate,
                $"Template line {index + 1} rate must be between 0 and 100.");
        }
    }
}

public sealed class CreateTaxTemplateCommandHandler
    : ICommandHandler<CreateTaxTemplateCommand, Result<TaxTemplateDto>>
{
    private readonly ITaxTemplateRepository _templates;
    private readonly IAccountRepository _accounts;
    private readonly ICompanyRepository _companies;

    public CreateTaxTemplateCommandHandler(
        ITaxTemplateRepository templates,
        IAccountRepository accounts,
        ICompanyRepository companies)
    {
        _templates = templates;
        _accounts = accounts;
        _companies = companies;
    }

    public async Task<Result<TaxTemplateDto>> HandleAsync(
        CreateTaxTemplateCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            var title = TaxTemplateGuard.NormalizeTitle(command.Title);
            var category = TaxTemplateGuard.NormalizeCategory(command.Category);

            if (await _templates.ExistsTitleAsync(company.Id, category, title, null, cancellationToken))
            {
                throw new TaxValidationException(
                    TaxErrorCodes.DuplicateTaxTemplate,
                    $"Tax template '{title}' already exists for this company and category.");
            }

            var lines = command.Lines ?? Array.Empty<TaxTemplateLineInput>();
            for (var i = 0; i < lines.Count; i++)
            {
                TaxTemplateGuard.EnsureValidLine(lines[i], i);
                await RequirePostingLeafAsync(lines[i].AccountId, company.Id, i, cancellationToken);
            }

            var template = new TaxTemplate
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Title = title,
                Category = category,
                IsDefault = command.IsDefault,
                Taxes = lines.Select((l, i) => new TaxTemplateItem
                {
                    Id = Guid.NewGuid(),
                    AccountId = l.AccountId,
                    Description = (l.Description ?? string.Empty).Trim(),
                    Rate = l.Rate,
                    Amount = l.Amount,
                    ChargeType = l.ChargeType,
                    CalculationBasis = l.CalculationBasis,
                }).ToList(),
            };

            if (template.IsDefault)
            {
                await ClearOtherDefaultsAsync(company.Id, category, template.Id, cancellationToken);
            }

            await _templates.AddAsync(template, cancellationToken);
            return Result<TaxTemplateDto>.Success(TaxTemplateDto.From(template));
        }
        catch (TaxValidationException ex)
        {
            return Result<TaxTemplateDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task RequirePostingLeafAsync(Guid accountId, Guid companyId, int index, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(accountId, ct);
        if (account is null || account.CompanyId != companyId || !account.IsActive || account.IsGroup)
        {
            throw new TaxValidationException(
                TaxErrorCodes.TaxAccountRequired,
                $"Template line {index + 1} account must be an active posting (leaf) account of this company.");
        }
    }

    private async Task ClearOtherDefaultsAsync(Guid companyId, string category, Guid keepId, CancellationToken ct)
    {
        var others = (await _templates.GetByCompanyCategoryAsync(companyId, category, ct))
            .Where(t => t.Id != keepId && t.IsDefault)
            .ToList();
        foreach (var other in others)
        {
            other.IsDefault = false;
            await _templates.UpdateAsync(other, ct);
        }
    }
}

public sealed class UpdateTaxTemplateCommandHandler
    : ICommandHandler<UpdateTaxTemplateCommand, Result<TaxTemplateDto>>
{
    private readonly ITaxTemplateRepository _templates;
    private readonly IAccountRepository _accounts;
    private readonly ICompanyRepository _companies;

    public UpdateTaxTemplateCommandHandler(
        ITaxTemplateRepository templates,
        IAccountRepository accounts,
        ICompanyRepository companies)
    {
        _templates = templates;
        _accounts = accounts;
        _companies = companies;
    }

    public async Task<Result<TaxTemplateDto>> HandleAsync(
        UpdateTaxTemplateCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _templates.GetByIdAsync(command.Id, cancellationToken)
                ?? throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{command.Id}' was not found in this tenant.");

            if (template.CompanyId != command.CompanyId)
            {
                throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{command.Id}' does not belong to this company.");
            }

            var title = TaxTemplateGuard.NormalizeTitle(command.Title);
            var category = TaxTemplateGuard.NormalizeCategory(command.Category);

            if (await _templates.ExistsTitleAsync(template.CompanyId, category, title, template.Id, cancellationToken))
            {
                throw new TaxValidationException(
                    TaxErrorCodes.DuplicateTaxTemplate,
                    $"Tax template '{title}' already exists for this company and category.");
            }

            var lines = command.Lines ?? Array.Empty<TaxTemplateLineInput>();
            for (var i = 0; i < lines.Count; i++)
            {
                TaxTemplateGuard.EnsureValidLine(lines[i], i);
                var account = await _accounts.GetByIdAsync(lines[i].AccountId, cancellationToken);
                if (account is null || account.CompanyId != template.CompanyId || !account.IsActive || account.IsGroup)
                {
                    throw new TaxValidationException(
                        TaxErrorCodes.TaxAccountRequired,
                        $"Template line {i + 1} account must be an active posting (leaf) account of this company.");
                }
            }

            template.Title = title;
            template.Category = category;
            template.IsDefault = command.IsDefault;
            template.Taxes = lines.Select(l => new TaxTemplateItem
            {
                Id = Guid.NewGuid(),
                TaxTemplateId = template.Id,
                AccountId = l.AccountId,
                Description = (l.Description ?? string.Empty).Trim(),
                Rate = l.Rate,
                Amount = l.Amount,
                ChargeType = l.ChargeType,
                CalculationBasis = l.CalculationBasis,
            }).ToList();

            if (template.IsDefault)
            {
                var others = (await _templates.GetByCompanyCategoryAsync(
                        template.CompanyId, category, cancellationToken))
                    .Where(t => t.Id != template.Id && t.IsDefault)
                    .ToList();
                foreach (var other in others)
                {
                    other.IsDefault = false;
                    await _templates.UpdateAsync(other, cancellationToken);
                }
            }

            await _templates.UpdateAsync(template, cancellationToken);
            return Result<TaxTemplateDto>.Success(TaxTemplateDto.From(template));
        }
        catch (TaxValidationException ex)
        {
            return Result<TaxTemplateDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class DeleteTaxTemplateCommandHandler
    : ICommandHandler<DeleteTaxTemplateCommand, Result<bool>>
{
    private readonly ITaxTemplateRepository _templates;

    public DeleteTaxTemplateCommandHandler(ITaxTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<Result<bool>> HandleAsync(
        DeleteTaxTemplateCommand command, CancellationToken cancellationToken = default)
    {
        var template = await _templates.GetByIdAsync(command.Id, cancellationToken);
        if (template is null || template.CompanyId != command.CompanyId)
        {
            return Result<bool>.Failure(
                TaxErrorCodes.TaxTemplateNotFound,
                $"Tax template '{command.Id}' was not found in this company.");
        }

        await _templates.DeleteAsync(template, cancellationToken);
        return Result<bool>.Success(true);
    }
}
