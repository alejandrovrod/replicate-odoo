using System;

namespace Erp.Application.DTOs;

public record ProjectDto(
    Guid Id,
    Guid CompanyId,
    string ProjectName,
    string ProjectType,
    string Status,
    DateOnly? ExpectedStartDate,
    DateOnly? ExpectedEndDate,
    Guid? CustomerId,
    decimal EstimatedCost,
    decimal PercentComplete,
    string? Notes,
    DateTimeOffset CreatedAt
);
