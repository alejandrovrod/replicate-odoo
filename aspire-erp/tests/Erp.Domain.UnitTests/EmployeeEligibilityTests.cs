using Erp.Domain.Entities;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 12.1 / spec invariant HR-03: the active-employee payroll eligibility matrix.
/// <see cref="Employee.IsEligibleForPeriod"/> implements the rule literally (Active &amp;&amp;
/// joined &lt;= end &amp;&amp; (not relieved || relieved &gt;= start), boundaries inclusive).
/// </summary>
public sealed class EmployeeEligibilityTests
{
    private static readonly DateOnly PeriodStart = new(2026, 10, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 10, 31);

    private static Employee Staff(
        EmploymentStatus status,
        DateOnly joining,
        DateOnly? relieving = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            EmployeeNumber = "EMP-001",
            FirstName = "Maria",
            LastName = "Santos",
            WorkEmail = "maria.santos@example.com",
            DateOfJoining = joining,
            DateOfRelieving = relieving,
            Status = status,
        };

    [Fact]
    public void Active_JoinedBeforePeriod_NoRelieving_IsEligible()
    {
        Assert.True(Staff(EmploymentStatus.Active, new DateOnly(2025, 1, 15))
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }

    [Theory]
    [InlineData(EmploymentStatus.Inactive)]
    [InlineData(EmploymentStatus.Suspended)]
    [InlineData(EmploymentStatus.Left)]
    public void NonActiveStatus_IsNeverEligible(EmploymentStatus status)
    {
        Assert.False(Staff(status, new DateOnly(2025, 1, 15))
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }

    [Fact]
    public void Active_JoinedAfterPeriodEnd_IsNotEligible()
    {
        Assert.False(Staff(EmploymentStatus.Active, new DateOnly(2026, 11, 1))
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }

    [Fact]
    public void Active_RelievedBeforePeriodStart_IsNotEligible()
    {
        Assert.False(Staff(
                EmploymentStatus.Active,
                new DateOnly(2025, 1, 15),
                new DateOnly(2026, 9, 30))
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }

    [Fact]
    public void Active_JoinedExactlyOnPeriodEnd_IsEligible_BoundaryInclusive()
    {
        Assert.True(Staff(EmploymentStatus.Active, PeriodEnd)
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }

    [Fact]
    public void Active_RelievedExactlyOnPeriodStart_IsEligible_BoundaryInclusive()
    {
        Assert.True(Staff(
                EmploymentStatus.Active,
                new DateOnly(2025, 1, 15),
                PeriodStart)
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }

    [Fact]
    public void Active_JoinedInsidePeriod_IsEligible()
    {
        Assert.True(Staff(EmploymentStatus.Active, new DateOnly(2026, 10, 15))
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }

    [Fact]
    public void Active_RelievedInsidePeriod_IsEligible()
    {
        Assert.True(Staff(
                EmploymentStatus.Active,
                new DateOnly(2025, 1, 15),
                new DateOnly(2026, 10, 15))
            .IsEligibleForPeriod(PeriodStart, PeriodEnd));
    }
}
