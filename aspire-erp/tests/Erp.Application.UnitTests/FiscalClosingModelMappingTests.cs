using Erp.Domain.Entities;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// R-13 Fase 2 (tasks.md Phase 2 acceptance, offline half): the EF design-time model for
/// <c>FiscalYears</c>, the rebuilt <c>PeriodClosingVouchers</c> and the new
/// <c>PeriodClosingVoucherLines</c> — tables, CHECKs, filtered uniques, FK targets and the
/// RowVersion tokens. Built with <see cref="ModelBuilder"/> over the SqlServer convention set:
/// full metadata, zero connection opened (the CrmModelMappingTests precedent). Live round-trips
/// (overlap race, double-submit, DB-level unique violations) need the SQL Server dev container.
/// </summary>
public sealed class FiscalClosingModelMappingTests
{
    private static IModel BuildDesignModel()
    {
        var modelBuilder = new ModelBuilder(SqlServerConventionSetBuilder.Build());
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        return modelBuilder.FinalizeModel();
    }

    [Fact]
    public void Model_ContainsFiscalClosingSets_WithExpectedTables()
    {
        var model = BuildDesignModel();

        Assert.Equal("FiscalYears", model.FindEntityType(typeof(FiscalYear))!.GetTableName());
        Assert.Equal("PeriodClosingVouchers", model.FindEntityType(typeof(PeriodClosingVoucher))!.GetTableName());
        Assert.Equal("PeriodClosingVoucherLines", model.FindEntityType(typeof(PeriodClosingVoucherLine))!.GetTableName());
    }

    [Fact]
    public void FiscalYearMapping_CarriesDateCheck_UniqueYear_AndRowVersion()
    {
        var year = BuildDesignModel().FindEntityType(typeof(FiscalYear))!;

        var checks = year.GetCheckConstraints().Select(c => c.Name).ToHashSet();
        Assert.Contains("CK_FY_Dates", checks);
        Assert.Contains("CK_FY_ClosedAt", checks);

        var unique = year.GetIndexes().Single(i => i.GetDatabaseName() == "UQ_FY_Company_Year");
        Assert.True(unique.IsUnique);

        Assert.True(year.FindProperty(nameof(FiscalYear.RowVersion))!.IsConcurrencyToken);
        // Column-type metadata (no runtime init in the offline model: read the annotation).
        Assert.Equal(
            "date",
            year.FindProperty(nameof(FiscalYear.StartDate))!.FindAnnotation("Relational:ColumnType")!.Value);
    }

    [Fact]
    public void PeriodClosingVoucherMapping_CarriesStatusCheck_FilteredUniques_AndFiscalYearLink()
    {
        var voucher = BuildDesignModel().FindEntityType(typeof(PeriodClosingVoucher))!;

        var checks = voucher.GetCheckConstraints().Select(c => c.Name).ToHashSet();
        Assert.Contains("CK_PCV_Status", checks);

        var indexes = voucher.GetIndexes().ToDictionary(i => i.GetDatabaseName()!);
        Assert.True(indexes["UQ_One_Submitted_Close_Per_Year"].IsUnique);
        Assert.NotNull(indexes["UQ_One_Submitted_Close_Per_Year"].GetFilter());
        Assert.True(indexes["UQ_PCV_Company_VoucherNo"].IsUnique);
        Assert.True(indexes["UQ_PCV_Idempotency"].IsUnique);
        Assert.NotNull(indexes["UQ_PCV_Idempotency"].GetFilter());

        Assert.True(voucher.FindProperty(nameof(PeriodClosingVoucher.RowVersion))!.IsConcurrencyToken);
        Assert.NotNull(voucher.FindProperty(nameof(PeriodClosingVoucher.FiscalYearId)));
        Assert.NotNull(voucher.FindProperty(nameof(PeriodClosingVoucher.IdempotencyKey)));

        var fkNames = voucher.GetForeignKeys().Select(fk => fk.GetConstraintName()).ToHashSet();
        Assert.Contains("FK_PCV_Company", fkNames);
        Assert.Contains("FK_PCV_FiscalYear", fkNames);
        Assert.Contains("FK_PCV_Retained", fkNames);
    }

    [Fact]
    public void PeriodClosingVoucherLineMapping_CarriesNonNegativeCheck()
    {
        var line = BuildDesignModel().FindEntityType(typeof(PeriodClosingVoucherLine))!;

        var checks = line.GetCheckConstraints().Select(c => c.Name).ToHashSet();
        Assert.Contains("CK_PCVL_NonNeg", checks);
    }
}
