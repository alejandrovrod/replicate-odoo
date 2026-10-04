using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FixedAssetAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccumulatedDepreciationAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepreciationExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CwipAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GainOnDisposalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LossOnDisposalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsNonDepreciable = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetCategory_AccumDep",
                        column: x => x.AccumulatedDepreciationAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCategory_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCategory_Cwip",
                        column: x => x.CwipAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCategory_DepExpense",
                        column: x => x.DepreciationExpenseAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCategory_FixedAsset",
                        column: x => x.FixedAssetAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCategory_GainDisposal",
                        column: x => x.GainOnDisposalAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCategory_LossDisposal",
                        column: x => x.LossOnDisposalAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AssetCategoryHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Asset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AvailableForUseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossPurchaseAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SalvageValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    DepreciationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "StraightLine"),
                    TotalNumberOfDepreciations = table.Column<int>(type: "int", nullable: false),
                    FrequencyInMonths = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    DisposalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asset", x => x.Id);
                    table.CheckConstraint("CK_Asset_Periods", "[TotalNumberOfDepreciations] > 0 AND [FrequencyInMonths] > 0");
                    table.CheckConstraint("CK_Asset_Values", "[GrossPurchaseAmount] > 0.0000 AND [SalvageValue] >= 0.0000 AND [AccumulatedDepreciation] >= 0.0000");
                    table.ForeignKey(
                        name: "FK_Asset_Category",
                        column: x => x.AssetCategoryId,
                        principalTable: "AssetCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asset_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asset_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AssetHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "AssetDepreciationSchedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepreciationAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AccumulatedDepreciationAfter = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Scheduled"),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDepreciationSchedule", x => x.Id);
                    table.CheckConstraint("CK_DepSchedule_Amount", "[DepreciationAmount] > 0.0000");
                    table.ForeignKey(
                        name: "FK_DepSchedule_Asset",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asset_AssetCategoryId",
                table: "Asset",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_CompanyId",
                table: "Asset",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_ItemId",
                table: "Asset",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Tenant_Category",
                table: "Asset",
                columns: new[] { "TenantId", "CompanyId", "AssetCategoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_AccumulatedDepreciationAccountId",
                table: "AssetCategory",
                column: "AccumulatedDepreciationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_CompanyId",
                table: "AssetCategory",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_CwipAccountId",
                table: "AssetCategory",
                column: "CwipAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_DepreciationExpenseAccountId",
                table: "AssetCategory",
                column: "DepreciationExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_FixedAssetAccountId",
                table: "AssetCategory",
                column: "FixedAssetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_GainOnDisposalAccountId",
                table: "AssetCategory",
                column: "GainOnDisposalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_LossOnDisposalAccountId",
                table: "AssetCategory",
                column: "LossOnDisposalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategory_Tenant_Company",
                table: "AssetCategory",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedule_AssetId",
                table: "AssetDepreciationSchedule",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepSchedule_Date",
                table: "AssetDepreciationSchedule",
                columns: new[] { "ScheduleDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetDepreciationSchedule");

            migrationBuilder.DropTable(
                name: "Asset")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AssetHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "AssetCategory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AssetCategoryHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
