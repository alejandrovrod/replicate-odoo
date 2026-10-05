using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="CreateSalaryStructureCommand"/> (Task 12.2): name + line shape rules
/// through the pure <see cref="SalaryStructureValidator"/>, then every referenced component is
/// resolved (exists + active + company-owned) before the header + lines persist in ONE
/// transaction. Component lookups ride the caller's cancellation token; the DTO is built from
/// the resolved components (no second read).
/// </summary>
public sealed class CreateSalaryStructureCommandHandler
    : ICommandHandler<CreateSalaryStructureCommand, Result<SalaryStructureDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IHrPayrollRepository _hr;

    public CreateSalaryStructureCommandHandler(
        ICompanyRepository companies,
        IHrPayrollRepository hr)
    {
        _companies = companies;
        _hr = hr;
    }

    public async Task<Result<SalaryStructureDto>> HandleAsync(
        CreateSalaryStructureCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SalaryStructureValidator.EnsureValidStructureName(command.StructureName);
            SalaryStructureValidator.EnsureValidLines(
                command.Lines
                    .Select(l => (l.Amount, l.PercentageOfBase))
                    .ToList());

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new HrValidationException(
                    HrPayrollErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            // Resolve EVERY component before the first write: a missing/inactive/foreign
            // component rejects the whole structure with zero rows persisted.
            var componentsById = new Dictionary<Guid, SalaryComponent>();
            foreach (var line in command.Lines)
            {
                if (!componentsById.ContainsKey(line.ComponentId))
                {
                    var component = await _hr.GetComponentByIdAsync(line.ComponentId, cancellationToken)
                        ?? throw new HrValidationException(
                            HrPayrollErrorCodes.ComponentNotFound,
                            $"Salary component '{line.ComponentId}' was not found in this tenant.");

                    if (component.CompanyId != company.Id)
                    {
                        throw new HrValidationException(
                            HrPayrollErrorCodes.ComponentNotFound,
                            $"Salary component '{component.ComponentName}' belongs to another company.");
                    }

                    if (!component.IsActive)
                    {
                        throw new HrValidationException(
                            HrPayrollErrorCodes.InactiveComponent,
                            $"Salary component '{component.ComponentName}' is inactive and cannot be priced into a structure.");
                    }

                    componentsById.Add(line.ComponentId, component);
                }
            }

            var structure = new SalaryStructure
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                StructureName = command.StructureName.Trim(),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,

                // TenantId is intentionally NOT set: AppDbContext stamps it on insert (II.4).
            };

            foreach (var line in command.Lines)
            {
                structure.Lines.Add(new SalaryStructureLine
                {
                    Id = Guid.NewGuid(),
                    StructureId = structure.Id,
                    ComponentId = line.ComponentId,
                    Amount = line.Amount,
                    PercentageOfBase = line.PercentageOfBase,
                });
            }

            var dto = SalaryStructureDto.Build(structure, componentsById);

            await _hr.ExecuteInTransactionAsync(
                async token =>
                {
                    await _hr.AddStructureAsync(structure, token);
                    return true;
                },
                cancellationToken);

            return Result<SalaryStructureDto>.Success(dto);
        }
        catch (HrValidationException ex)
        {
            return Result<SalaryStructureDto>.Failure(ex.Code, ex.Message);
        }
    }
}
