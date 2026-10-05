using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="CreateSalaryComponentCommand"/> (Task 12.2 acceptance: "all components
/// map to valid leaf posting accounts"): field validation + the Constitution III.3
/// leaf-posting guard (exists + active + IsGroup == false + company-owned) through
/// <see cref="HrPayroll.HrAccountGuards"/>. Mirrors CreateAssetCategoryCommandHandler's guard
/// usage.
/// </summary>
public sealed class CreateSalaryComponentCommandHandler
    : ICommandHandler<CreateSalaryComponentCommand, Result<SalaryComponentDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IHrPayrollRepository _hr;

    public CreateSalaryComponentCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IHrPayrollRepository hr)
    {
        _companies = companies;
        _accounts = accounts;
        _hr = hr;
    }

    public async Task<Result<SalaryComponentDto>> HandleAsync(
        CreateSalaryComponentCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SalaryStructureValidator.EnsureValidComponentName(command.ComponentName);

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new HrValidationException(
                    HrPayrollErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            await HrAccountGuards.RequirePostableAccountAsync(
                _accounts, command.DefaultGLAccountId, company.Id, "default GL account", cancellationToken);

            var component = new SalaryComponent
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                ComponentName = command.ComponentName.Trim(),
                ComponentType = command.ComponentType,
                DependsOnPaymentDays = command.DependsOnPaymentDays,
                IsTaxApplicable = command.IsTaxApplicable,
                DefaultGLAccountId = command.DefaultGLAccountId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,

                // TenantId is intentionally NOT set: AppDbContext stamps it on insert (II.4).
            };

            await _hr.AddComponentAsync(component, cancellationToken);

            return Result<SalaryComponentDto>.Success(SalaryComponentDto.Build(component));
        }
        catch (HrValidationException ex)
        {
            return Result<SalaryComponentDto>.Failure(ex.Code, ex.Message);
        }
    }
}
