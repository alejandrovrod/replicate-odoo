using System;
using System.Collections.Generic;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum ProjectStatus
{
    Draft = 1,
    Open = 2,
    Completed = 3,
    Cancelled = 4
}

public class Project : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ProjectName { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; } = ProjectStatus.Open;
    
    public DateOnly? ExpectedStartDate { get; set; }
    public DateOnly? ExpectedEndDate { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    public decimal EstimatedCost { get; set; }
    public decimal PercentComplete { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ProjectTask : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public DateOnly? ExpectedStartDate { get; set; }
    public DateOnly? ExpectedEndDate { get; set; }
    
    public decimal TaskWeight { get; set; } = 1.0m;

    public DateTimeOffset CreatedAt { get; set; }
}
