using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Projects.Commands;

public sealed class CreateProjectCommandHandler : ICommandHandler<CreateProjectCommand, Result<ProjectDto>>
{
    private readonly IProjectRepository _projects;

    public CreateProjectCommandHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result<ProjectDto>> HandleAsync(CreateProjectCommand request, CancellationToken cancellationToken = default)
    {
        var entity = new Project
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            ProjectName = request.ProjectName,
            ProjectType = request.ProjectType,
            ExpectedStartDate = request.ExpectedStartDate,
            ExpectedEndDate = request.ExpectedEndDate,
            CustomerId = request.CustomerId,
            EstimatedCost = request.EstimatedCost,
            Notes = request.Notes,
            Status = ProjectStatus.Open,
            PercentComplete = 0m
        };

        await _projects.AddAsync(entity, cancellationToken);

        var dto = new ProjectDto(
            entity.Id,
            entity.CompanyId,
            entity.ProjectName,
            entity.ProjectType,
            entity.Status.ToString(),
            entity.ExpectedStartDate,
            entity.ExpectedEndDate,
            entity.CustomerId,
            entity.EstimatedCost,
            entity.PercentComplete,
            entity.Notes,
            DateTimeOffset.UtcNow
        );

        return Result<ProjectDto>.Success(dto);
    }
}
