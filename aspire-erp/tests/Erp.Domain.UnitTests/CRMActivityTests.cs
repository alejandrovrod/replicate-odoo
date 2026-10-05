using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using System;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Block A-fill tests for the plan §1 CRMActivity follow-up log (the table task 11.4's
/// audit-trail acceptance requires). Pure-domain: entity defaults + validator bounds.
/// EF mapping (config/DbSet/repository) is verified by build + Block C integration tests.
/// </summary>
public class CRMActivityTests
{
    [Fact]
    public void NewActivity_ShouldDefaultToNoteTypeAndCurrentTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var activity = new CRMActivity();
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(CRMActivityType.Note, activity.Type);
        Assert.InRange(activity.ActivityDate, before, after);
    }

    [Fact]
    public void EnsureValidActivityFields_ShouldAcceptWellFormedActivity()
    {
        var ex = Record.Exception(() => CRMActivityValidator.EnsureValidActivityFields(
            Guid.NewGuid(), "Follow-up call with CTO", Guid.NewGuid()));

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureValidActivityFields_WithEmptyOpportunity_ShouldThrow()
    {
        var ex = Assert.Throws<CRMValidationException>(() =>
            CRMActivityValidator.EnsureValidActivityFields(
                Guid.Empty, "Follow-up call", Guid.NewGuid()));

        Assert.Equal("crm_activity_opportunity_required", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValidActivityFields_WithMissingSubject_ShouldThrow(string? subject)
    {
        var ex = Assert.Throws<CRMValidationException>(() =>
            CRMActivityValidator.EnsureValidActivityFields(
                Guid.NewGuid(), subject, Guid.NewGuid()));

        Assert.Equal("crm_activity_subject_invalid", ex.Code);
    }

    [Fact]
    public void EnsureValidActivityFields_WithSubjectOver200Chars_ShouldThrow()
    {
        var ex = Assert.Throws<CRMValidationException>(() =>
            CRMActivityValidator.EnsureValidActivityFields(
                Guid.NewGuid(), new string('x', 201), Guid.NewGuid()));

        Assert.Equal("crm_activity_subject_invalid", ex.Code);
    }

    [Fact]
    public void EnsureValidActivityFields_WithEmptyAuthor_ShouldThrow()
    {
        var ex = Assert.Throws<CRMValidationException>(() =>
            CRMActivityValidator.EnsureValidActivityFields(
                Guid.NewGuid(), "Follow-up call", Guid.Empty));

        Assert.Equal("crm_activity_author_required", ex.Code);
    }

    [Fact]
    public void Opportunity_ShouldExposeEmptyActivitiesCollectionByDefault()
    {
        var opportunity = new Opportunity();

        Assert.NotNull(opportunity.Activities);
        Assert.Empty(opportunity.Activities);
    }
}
