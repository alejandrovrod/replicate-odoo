using Erp.Domain.Entities;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Linq;
using Xunit;

namespace Erp.Application.UnitTests.Crm;

/// <summary>
/// Offline EF model-shape tests for the Block A-fill CRM mappings (Lead/Opportunity/
/// CRMActivity configurations). The model is built with <see cref="ModelBuilder"/> over the
/// SqlServer convention set - full design-time metadata, zero connection opened - so temporal
/// settings, CHECK names, the RowVersion token and the activity cascade are verified here;
/// live round-trips are Block C work.
/// </summary>
public class CrmModelMappingTests
{
    private static IModel BuildDesignModel()
    {
        var modelBuilder = new ModelBuilder(SqlServerConventionSetBuilder.Build());
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        return modelBuilder.FinalizeModel();
    }

    [Fact]
    public void Model_ShouldContainCrmSets_WithTemporalHistoryTables()
    {
        var model = BuildDesignModel();

        var lead = model.FindEntityType(typeof(Lead));
        var opportunity = model.FindEntityType(typeof(Opportunity));
        var activity = model.FindEntityType(typeof(CRMActivity));

        Assert.NotNull(lead);
        Assert.NotNull(opportunity);
        Assert.NotNull(activity);

        Assert.Equal("LeadHistory", lead!.GetHistoryTableName());
        Assert.Equal("OpportunityHistory", opportunity!.GetHistoryTableName());
        Assert.Null(activity!.GetHistoryTableName());
    }

    [Fact]
    public void OpportunityMapping_ShouldCarryRowVersionTokenAndPlanChecks()
    {
        var opportunity = BuildDesignModel().FindEntityType(typeof(Opportunity))!;

        Assert.True(opportunity.FindProperty(nameof(Opportunity.RowVersion))!.IsConcurrencyToken);

        var checks = opportunity.GetCheckConstraints().Select(c => c.Name).ToHashSet();
        Assert.Contains("CK_Opportunity_Amount", checks);
        Assert.Contains("CK_Opportunity_Probability", checks);

        // WeightedAmount is a computed CLR property (spec CRM-01), not a mapped column.
        Assert.Null(opportunity.FindProperty(nameof(Opportunity.WeightedAmount)));

        var indexNames = opportunity.GetIndexes().Select(i => i.GetDatabaseName()).ToHashSet();
        Assert.Contains("IX_Opportunity_Tenant_Stage", indexNames);
    }

    [Fact]
    public void LeadMapping_ShouldCarryUniqueCodeAndStatusIndex()
    {
        var lead = BuildDesignModel().FindEntityType(typeof(Lead))!;

        var codeIndex = lead.GetIndexes().Single(i => i.GetDatabaseName() == "UQ_Lead_Tenant_Company_Code");
        Assert.True(codeIndex.IsUnique);

        var indexNames = lead.GetIndexes().Select(i => i.GetDatabaseName()).ToHashSet();
        Assert.Contains("IX_Lead_Tenant_Status", indexNames);
    }

    [Fact]
    public void ActivityMapping_ShouldCascadeFromOpportunity_WithEnumNameType()
    {
        var activity = BuildDesignModel().FindEntityType(typeof(CRMActivity))!;

        var fk = Assert.Single(activity.GetForeignKeys());
        Assert.Equal(typeof(Opportunity), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        Assert.Equal("FK_CRMActivity_Opportunity", fk.GetConstraintName());

        // Enum persisted as its NVARCHAR name (AccountConfiguration RootType precedent):
        // the provider-side CLR type is string, never int.
        Assert.Equal(
            typeof(string),
            activity.FindProperty(nameof(CRMActivity.Type))!.GetProviderClrType());

        Assert.Equal(200, activity.FindProperty(nameof(CRMActivity.Subject))!.GetMaxLength());
    }
}
