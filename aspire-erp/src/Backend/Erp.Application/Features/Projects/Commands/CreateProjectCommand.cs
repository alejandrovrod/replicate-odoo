using System;
using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Projects.Commands;

public sealed record CreateProjectCommand(
    Guid CompanyId,
    string ProjectName,
    string ProjectType = "",
    DateOnly? ExpectedStartDate = null,
    DateOnly? ExpectedEndDate = null,
    Guid? CustomerId = null,
    decimal EstimatedCost = 0m,
    string? Notes = null
) : ICommand<Result<ProjectDto>>;
